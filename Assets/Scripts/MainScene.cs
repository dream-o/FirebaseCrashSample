using System.Collections;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

public class MainScene : MonoBehaviour
{
    private const int ADD_COUNT = 10;
    [SerializeField] private GameObject curtain = null;
    
    private LogScheduler logScheduler = null;
    public static bool isInitializedFirebase = false;
    private Firebase.Auth.FirebaseAuth firebaseAuth = null;

    private void Awake()
    {
        logScheduler = new LogScheduler();
        logScheduler.Initialize();
        
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread((status) =>
        {
            logScheduler.dependencyStatus = status.Result;
            isInitializedFirebase = true;
        });
    }

    private IEnumerator Start()
    {
        var yieldInstruction = new WaitForSeconds(0.1F);
        while (!isInitializedFirebase)
        {
            yield return yieldInstruction;
        }
        
        curtain.SetActive(false);
        InitializeAuth();
    }

    void InitializeAuth()
    {
        firebaseAuth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (firebaseAuth == null)
        {
            Debug.LogError("Invalid Auth Instance");
            return;
        }

        firebaseAuth.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null);
        if (firebaseAuth.CurrentUser == null)
        {
            LoginAnonymously();
        }
    }

    private void OnDestroy()
    {
        if (logScheduler != null)
        {
            logScheduler.Destroy();
        }

        if (firebaseAuth != null)
        {
            firebaseAuth.StateChanged -= AuthStateChanged;
        }
    }

    void AuthStateChanged(object sender_, System.EventArgs eventArgs_)
    {
        if (firebaseAuth != null && firebaseAuth.CurrentUser != null)
        {
            logScheduler.CanSendLog = true;
        }
    }

    void LoginAnonymously()
    {
        Debug.Log("Try Login Anonymously");

        firebaseAuth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Task(Login Anonymously) is faulted or canceld!");
                return;
            }

            Debug.Log($"Task(Login Anonymously) is success.");
            AuthStateChanged(this, null);
        });
    }

    public void HandleClickedWriteButton()
    {
        for (int i = 0; i < ADD_COUNT; ++i)
        {
            logScheduler.Write();
        }
    }
}