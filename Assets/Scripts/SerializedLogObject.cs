[System.Serializable]
public class SerializedLogObject
{
    public string dateTime;
    public int sequence;
    public InnerSerializedLogObject innerObject = null;
}

[System.Serializable]
public class InnerSerializedLogObject
{
    public string dummyString;
    public int testFlag;
}