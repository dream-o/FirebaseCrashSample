using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Firebase;
using Firebase.Extensions;
using Firebase.Storage;
using UnityEngine;

public class LogScheduler
{
    private const int MAX_LOGFILE_SIZE = 5000;
    private const string LOG_DIR = "LogFiles";
    private const string BASE_REMOTE_PATH = "Root";
    private int sequencialNumber = 0;
    private byte[] logBytes = null;
    private Thread logThread = null;
    private bool isSending = false;
    private string persistentDataPath;

    public Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavilableMissing;
    public bool CanSendLog { get; set; } = false;

    public void Initialize()
    {
        persistentDataPath = Application.persistentDataPath;
        
        string logDir = GetLogDir();
        if (false == Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        logBytes = new byte[MAX_LOGFILE_SIZE];
        
        RestoreTempFiles();
        InitializeThread();
    }

    public void Destroy()
    {
        if (logThread != null)
        {
            logThread.Abort();
        }
    }
    
    private void InitializeThread()
    {
        logThread = new Thread(ProcessSendLog);
        logThread.IsBackground = true;
        logThread.Start();
    }

    private void RestoreTempFiles()
    {
        var files = Directory.GetFiles(GetLogDir(), "*.tmp");

        for (int i = 0; i < files.Length; ++i)
        {
            string destination = files[i].Replace(".tmp", string.Empty);
            if (File.Exists(files[i]))
            {
                File.Move(files[i], destination);
            }
        }
    }

    private string GetLogDir()
    {
        return Path.Combine(persistentDataPath, LOG_DIR);
    }

    public void Write()
    {
        var now = System.DateTime.UtcNow;
        
        SerializedLogObject serializedLogObject = new SerializedLogObject();
        serializedLogObject.sequence = sequencialNumber;
        serializedLogObject.dateTime = $"{now.Year}{now.Month:00}{now.Day:00}{now.Hour:00}{now.Minute:00}{now.Second:00}";

        var innerObject = new InnerSerializedLogObject();
        innerObject.dummyString = "blahblah";
        innerObject.testFlag = UnityEngine.Random.Range(0, 100);

        serializedLogObject.innerObject = innerObject;

        string fileName = $"{serializedLogObject.dateTime}_{sequencialNumber}.gz";
        string filePath = Path.Combine(GetLogDir(), fileName);

        string contents = JsonUtility.ToJson(serializedLogObject);
        
        int byteLength = System.Text.Encoding.UTF8.GetBytes(contents, 0, contents.Length, logBytes, 0);
 
        using (FileStream compressedStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write))
        {
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                zipStream.Write(logBytes, 0, byteLength);
            }
        }

        ++sequencialNumber;
    }

    private void ProcessSendLog()
    {
        while (!MainScene.isInitializedFirebase)
        {
            Thread.Sleep(TimeSpan.FromSeconds(0.5F));
        }

        if (dependencyStatus != DependencyStatus.Available)
        {
            Debug.LogWarning("Can't upload log to firebase");
            return;
        }

        while (!CanSendLog)
        {
            Thread.Sleep(TimeSpan.FromSeconds(1F));
        }

        while (true)
        {
            Thread.Sleep(TimeSpan.FromSeconds(1F));
 
            var files = Directory.GetFiles(GetLogDir(), "*.gz");
            if (files.Length < 1)
            {
                continue;
            }
            
            if (isSending)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1F));
                continue;
            }

            var targetFile = files[0];
            SendLogByStorage(targetFile);
        }
    }

    string GetRemotePath(string localPath)
    {
        return $"{BASE_REMOTE_PATH}/{Path.GetFileName(localPath)}";
    }

    private void SendLogByStorage(string localPath)
    {
        isSending = true;
        Debug.Log($"localPath:{localPath}");

        var metaData = new MetadataChange();
        metaData.ContentType = "application/x-gzip";

        string remotePath = GetRemotePath(localPath);
        var reference = Firebase.Storage.FirebaseStorage.DefaultInstance.RootReference.Child(remotePath);
        
        var progressMonitor = new Firebase.Storage.StorageProgress<UploadState>(OnChangedProgressUpload);

        reference.PutFileAsync(localPath, metaData, progressMonitor, CancellationToken.None).ContinueWithOnMainThread(
            (task) =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    if (File.Exists(localPath))
                    {
                        File.Move(localPath, $"{localPath}.tmp");
                    }

                    Debug.LogError("Task(Upload Log) is faulted or canceld!");
                }
                else
                {
                    Debug.Log($"Task(Upload Log) is success.");
                
                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }
                }

                isSending = false;
            });
    }
    
    private void OnChangedProgressUpload(UploadState value)
    {
        if (value != null)
        {
            string refName = "empty";
            if (value.Reference != null && !string.IsNullOrEmpty(value.Reference.Name))
            {
                refName = value.Reference.Name;
            }
                
            Debug.Log($"OnChangeProgressUpload:{refName}, bytes:{value.BytesTransferred}, total:{value.TotalByteCount}");
        }
        else
        {
            Debug.Log($"OnChangeProgressUpload");
        }
    }
}
