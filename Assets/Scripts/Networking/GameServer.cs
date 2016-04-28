﻿using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameServer : MonoBehaviour {

    private byte channelReliable;
    private int maxConnections = 4;

    private int port = 8888;
    private int key = 420;
    private int version = 1;
    private int subversion = 0;

    private Level level;

    private int serverSocket = -1;
    private List<int> clientConnections = new List<int>();

    void OnEnable() {
        Application.runInBackground = true; // for debugging purposes
        Destroy(gameObject.GetComponent<GameClient>());
        DontDestroyOnLoad(gameObject);

        // for testing until we get database working
        PlayerPrefs.DeleteAll();

        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        channelReliable = config.AddChannel(QosType.Reliable);
        HostTopology topology = new HostTopology(config, maxConnections);

        serverSocket = NetworkTransport.AddHost(topology, port);
        Debug.Log("SERVER: socket opened: " + serverSocket);

        Packet p = MakeTestPacket();

        byte error;
        bool b = NetworkTransport.StartBroadcastDiscovery(
                     serverSocket, port - 1, key, version, subversion, p.getData(), p.getSize(), 500, out error);

        if (!b) {
            Debug.Log("SERVER: start broadcast discovery failed!");
            Application.Quit();
        } else if (NetworkTransport.IsBroadcastDiscoveryRunning()) {
            Debug.Log("SERVER: started and broadcasting");
        } else {
            Debug.Log("SERVER: started but not broadcasting!");
        }

        SceneManager.LoadScene(1);
    }

    Packet MakeTestPacket() {
        Packet p = new Packet(PacketType.MESSAGE);
        p.Write("HI ITS ME THE SERVER CONNECT UP");
        p.Write(23.11074f);
        p.Write(new Vector3(2.0f, 1.0f, 0.0f));
        return p;
    }

    // Update is called once per frame
    void Update() {
        checkMessages();

    }

    void OnLevelWasLoaded(int levelNum) {
        GameObject levelGO = GameObject.Find("Level");
        if (levelGO) {
            level = levelGO.GetComponent<Level>();
            level.GenerateLevel();
        }
    }

    public void sendPacket(Packet p, int clientID) {
        byte error;
        NetworkTransport.Send(serverSocket, clientID, channelReliable, p.getData(), p.getSize(), out error);
    }

    public void broadcastPacket(Packet packet) {
        for (int i = 0; i < clientConnections.Count; i++) {
            sendPacket(packet, clientConnections[i]);
        }
    }


    void checkMessages() {
        int recConnectionId;    // rec stands for received
        int recChannelId;
        int bsize = 1024;
        byte[] buffer = new byte[bsize];
        int dataSize;
        byte error;

        while (true) {
            NetworkEventType recEvent = NetworkTransport.ReceiveFromHost(
                serverSocket, out recConnectionId, out recChannelId, buffer, bsize, out dataSize, out error);
            switch (recEvent) {
                case NetworkEventType.Nothing:
                    return;
                case NetworkEventType.DataEvent:
                    receivePacket(new Packet(buffer), recConnectionId);
                    break;
                case NetworkEventType.ConnectEvent:
                    clientConnections.Add(recConnectionId);
                    Debug.Log("SERVER: client connected: " + recConnectionId);
                    break;
                case NetworkEventType.DisconnectEvent:
                    clientConnections.Remove(recConnectionId);
                    Debug.Log("SERVER: client disconnected: " + recConnectionId);
                    break;
                default:
                    break;

            }
        }

    }

    void receivePacket(Packet packet, int clientSocket) {
        PacketType pt = (PacketType)packet.ReadByte();
        switch (pt) {
            case PacketType.LOGIN:
                string name = packet.ReadString();
                string password = packet.ReadString();
                bool success = true;
                if (PlayerPrefs.HasKey(name)) {
                    if (password == PlayerPrefs.GetString(name)) {
                        Debug.Log("SERVER: player login accepted");
                    } else {
                        success = false;
                        Debug.Log("SERVER: player login denied, wrong password");
                    }
                } else {
                    Debug.Log("SERVER: new player \"" + name + "\" joined with password \"" + password + "\"");
                    PlayerPrefs.SetString(name, password);
                }

                // send login response back to client
                Packet p = new Packet(PacketType.LOGIN, 4096);
                if (success) {
                    p.Write(clientSocket);
                    int[] tiles = level.getTiles();
                    p.Write(tiles.Length);
                    for (int i = 0; i < tiles.Length; i++) {
                        p.Write((byte)tiles[i]);
                    }
                } else {
                    p.Write(-1);
                }
                sendPacket(p, clientSocket);

                break;
            default:
                break;
        }

    }
}
