using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EndPointData
{
    public EndPointName endPointName;
    public string endPointUrl;
    public bool usePrefix = true;
}
public enum ServerType
{
    Local,
    Live
}
public enum EndPointName
{
    CCTVData,
    BroadcastAuth,
    VTSVesselHydration,
    Berth,
    Vehicles,
    Traffic
}
[Serializable]
public class ServerConfig
{
    public string url;
    public string secretKey;
    public string accessToken;
    public ServerType serverType;
}

[CreateAssetMenu(menuName = "Data/ServerData")]
public class ServerData : ScriptableObject
{
    public ServerConfig[] serverConfigs;
    public string apiPrefix = "/api/v1";
    
    [HideInInspector]
    public string currentUrl;
    public ServerType serverType;
    public List<EndPointData> endPointDatas;
    public void OnEnable()
    {
        Validata();
    }
    public void Validata()
    {
        ChangeUrl(serverType);
    }
    
    public void ChangeUrl(ServerType serverType)
    {
        ServerConfig config = Array.Find(serverConfigs, s => s.serverType == serverType);
        currentUrl = config.url;
    }

    public ServerConfig GetServerConfig()
    {
        return Array.Find(serverConfigs, s => s.serverType == serverType);
    }

    public string PrepareUrl(EndPointName endPointName)
    {
        return GetFullUrl(endPointName);
    }

    public string GetFullUrl(EndPointName name)
    {
        ChangeUrl(serverType);
        EndPointData data = endPointDatas.Find(e => e.endPointName == name);
        if (data == null)
        {
            Debug.LogError($"Endpoint {name} not found in ServerData!");
            return string.Empty;
        }

        string prefix = data.usePrefix ? apiPrefix : "";
        return currentUrl + prefix + data.endPointUrl;
    }
}