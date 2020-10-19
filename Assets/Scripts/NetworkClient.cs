using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;


    public GameObject playerGO; // our player object
    public string myAddress; // my address = (IP, PORT)
    public Dictionary<string, GameObject> currentPlayers; // A list of currently connected players
    public List<string> newPlayers, droppedPlayers; // a list of new players, and a list of dropped players
    public PlayerUpdateMsg latestGameState; // the last game state received from server
    public ServerUpdateMsg initialSetofPlayers; // initial set of players to spawn
    //bool p_test = true; 


    void Start()
    {
        newPlayers = new List<string>();
        droppedPlayers = new List<string>();
        currentPlayers = new Dictionary<string, GameObject>();
        initialSetofPlayers = new ServerUpdateMsg();

        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP, serverPort);
        m_Connection = m_Driver.Connect(endpoint);
    }

    void SendToServer(string message)
    {
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message), Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect()
    {
        Debug.Log("We are now connected to the server");

        //// Example to send a handshake message:
        HandshakeMsg m = new HandshakeMsg();
        m.player.id = m_Connection.InternalId.ToString();
        SendToServer(JsonUtility.ToJson(m));
    } 
    void SpawnPlayers()
    {
        if (newPlayers.Count > 0)
        {
            foreach (string playerID in newPlayers)
            {
                currentPlayers.Add(playerID, Instantiate(playerGO, new Vector3(0, 0, 0), Quaternion.identity));
                currentPlayers[playerID].name = playerID;
                //Had an issue when multiple players were in the game you'd be able to control them locally now it adds a controler to specific players
                if (playerID == myAddress)
                {
                    currentPlayers[playerID].AddComponent<PlayerController>();
                }
            }
            newPlayers.Clear();
        }
        if (initialSetofPlayers.players.Count > 0)
        {
            Debug.Log(initialSetofPlayers);
            foreach (NetworkObjects.NetworkPlayer player in initialSetofPlayers.players)
            {
                if (player.id == myAddress)
                    continue;
                currentPlayers.Add(player.id, Instantiate(playerGO, new Vector3(0, 0, 0), Quaternion.identity));
                currentPlayers[player.id].GetComponent<Renderer>().material.color = new Color(player.cubeColor.r, player.cubeColor.g, player.cubeColor.b);
                currentPlayers[player.id].name = player.id;

            }
            initialSetofPlayers.players = new List<NetworkObjects.NetworkPlayer>();
        }
    }

    void UpdatePlayers()
    {
        if (latestGameState.player.players.Length > 0)
        {
            foreach (NetworkObjects.NetworkPlayer player in latestGameState.player.players)
            {
                string playerID = player.id;
                //No more disco cube
                //currentPlayers[player.id].GetComponent<Renderer>().material.color = new Color(player.color.R, player.color.G, player.color.B);
                //This makes sure that the server doesn't change my position back to where it was 
                if (player.id != myAddress)
                {   //This is how you see the updated position of other players
                    currentPlayers[player.id].GetComponent<Transform>().position = new Vector3(player.cubPos.x, player.cubPos.y, player.cubPos.z);
                }
            }
            //Sending the server information of our cubes position so that they can update other cubes of our location. Pretty cool man!
            foreach (NetworkObjects.NetworkPlayer player in latestGameState.player.players)
            {
                if (player.id == myAddress)
                {    //Getting the transform of the players cube to send to server 
                    LocalPositionData playerLocalPosition = new LocalPositionData();
                    playerLocalPosition.localPos.x = currentPlayers[player.id].GetComponent<Transform>().position.x;
                    playerLocalPosition.localPos.y = currentPlayers[player.id].GetComponent<Transform>().position.y;
                    playerLocalPosition.localPos.z = currentPlayers[player.id].GetComponent<Transform>().position.z;
                    //Making the position into a json and sending it to the server
                    string positionBytes = JsonUtility.ToJson(playerLocalPosition);
                    Byte[] sendingPositionBytes = Encoding.UTF8.GetBytes(positionBytes);
                    udp.Send(sendingPositionBytes, sendingPositionBytes.Length);
                }
            }
            latestGameState.player.players = new [0];
        }
    }


    //void OnInput()
    //{
    //    Debug.Log("Sending server message");

    //    //// Example to send a handshake message:
    //    int i = 10;
    //    PlayerInputMsg m = new PlayerInputMsg(i);
    //    m = m_Connection.InternalId.ToString();
    //    SendToServer(JsonUtility.ToJson(m));
    //}

    void OnData(DataStreamReader stream)
    {
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length, Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch (header.cmd)
        {
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                newPlayers.Add(hsMsg.player.id);
                myAddress = hsMsg.player.id;
                break;
            case Commands.PLAYER_UPDATE:
                //PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                latestGameState = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                
                Debug.Log("Player update message received!");
                break;
            case Commands.SERVER_UPDATE:
               initialSetofPlayers = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server update message received!");
                Debug.Log("PlayerList: " + initialSetofPlayers.players[0].id);
                break;
            default:
                Debug.Log("Unrecognized message received!");
                break;
        }
    }

    void Disconnect()
    {
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect()
    {
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }

   
    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
                
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
            
        }

        SpawnPlayers();
    }
    [Serializable]
    public struct receivedColor
    {
        public float R;
        public float G;
        public float B;
    }

    //Struct with variables to hold other players positions 
    [Serializable]
    public struct otherPlayerPosition
    {
        public float x;
        public float y;
        public float z; //Not using the z axis but including it for practice
    }
    //Struct for sending position to server
    [Serializable]
    public struct sentLocalPos
    {
        public float x;
        public float y;
        public float z;//Not using the z axis but including it for practice
    }

    [Serializable]
    public class LocalPositionData
    {

        public sentLocalPos localPos;

    }
    /// <summary>
    /// A structure that replicates our player dictionary on server
    /// </summary>
    [Serializable]
    public class Player
    {
        public string id;
        public receivedColor color;
        public otherPlayerPosition pos;

    }

    [Serializable]
    public class ListOfPlayers
    {
        public Player[] players;

        public ListOfPlayers()
        {
            players = new Player[0];
        }
    }
    [Serializable]
    public class GameState
    {
        public int pktID;
        public Player[] players;
    }
}