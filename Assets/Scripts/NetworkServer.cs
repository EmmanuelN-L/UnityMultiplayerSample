using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using System;
using System.Text;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;

    public PlayerUpdateMsg playerInfo = new PlayerUpdateMsg();
    public GameUpdateMsg playerList = new GameUpdateMsg();
    public GameUpdateMsg playerUpdateInfo = new GameUpdateMsg();

    //public List<>;

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
    }

    void SendToClient(string message, NetworkConnection c){
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void OnData(DataStreamReader stream, int i){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
            HandshakeMsg hs_Msg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
            Debug.Log("Handshake message received!");
            break;
            //Receiving the player update positions and ID
            case Commands.PLAYER_UPDATE:
            playerInfo= JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            Debug.Log("Player update message received!");

            for (i = 0; i < playerList.GameUpdate.Count; i++)
            {
                if (playerInfo.player.id == playerList.GameUpdate[i].id)
                {
                    //Adding updated infor and removing the old
                    //Not sure if it's the most efficient but it works
                    playerList.GameUpdate.Insert(i, playerInfo.player);
                    playerList.GameUpdate.RemoveAt(i + 1);  
                }
            }
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received!");
            break;
            //case Commands.PLAYER_INPUT:
            //PlayerInputMsg pInput = JsonUtility.FromJson<PlayerInputMsg>(recMsg);
            //Debug.Log("Received Input"+ pInput);
            //break;
            default:
            Debug.Log("SERVER ERROR: Unrecognized message received!");
            break;
        }
    }  
    void OnConnect(NetworkConnection c){
        m_Connections.Add(c);
        Debug.Log("Accepted a connection");

        //ServerUpdateMsg s_UpdateMessage = new ServerUpdateMsg();
        //for (int i = 0; i < 1; i++)
        //{
            
        //    tempID.id = c.InternalId.ToString();
        //    s_UpdateMessage.players.Add(tempID);
        //}
        
       // NetworkObjects.NetworkPlayer fakeID = new NetworkObjects.NetworkPlayer();


        NetworkObjects.NetworkPlayer realId = new NetworkObjects.NetworkPlayer();
        //Setting the players ID. E.g. Player 1 ID = 0
        realId.id = c.InternalId.ToString(); 
        //realId.cubeColor = playerData.player.cubeColor;
        realId.cubPos = playerInfo.player.cubPos;
        //Adding player ID to the player list
        playerList.GameUpdate.Add(realId);
        //Sending it to the client for a updated list
        SendToClient(JsonUtility.ToJson(playerList), c);

        //Sending the player their own internalID so they have a reference
        ConnectionAcceptedMsg ca_Msg = new ConnectionAcceptedMsg();
        NetworkObjects.NetworkPlayer connectID = new NetworkObjects.NetworkPlayer();
        connectID.id = c.InternalId.ToString();
        ca_Msg.player.Add(connectID);
        SendToClient(JsonUtility.ToJson(ca_Msg), c);
        
        //Sending the player a list of currently connected users
        ServerUpdateMsg suM = new ServerUpdateMsg();
        //Looping through all currently connected users
        //fakeId was was just for fun
        for (int i = 0; i < m_Connections.Length; i++)
        {
            NetworkObjects.NetworkPlayer fakeId = new NetworkObjects.NetworkPlayer();
            fakeId.id = playerList.GameUpdate[i].id;
            suM.players.Add(fakeId);
        }
        SendToClient(JsonUtility.ToJson(suM), c);

        // This is essentially a player joined message so now every client is aware of the new one who joined
        HandshakeMsg hs_Msg = new HandshakeMsg();
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (c.InternalId.ToString() != playerList.GameUpdate[i].id)
            {
                realId.id = c.InternalId.ToString();
                hs_Msg.player.Add(realId);
                SendToClient(JsonUtility.ToJson(hs_Msg), m_Connections[i]);
            }
        }

        //This is some stuff I had before but i've upgraded the code

        //Debug.Log("Got This -> " + s_UpdateMessage.players[0].id);
        //suM.playerlist.players = c.InternalId.ToString();

        //SendToClient(JsonUtility.ToJson(s_UpdateMessage), c);

        ////// Example to send a handshake message:
        //HandshakeMsg m = new HandshakeMsg();
        //m.player.id = c.InternalId.ToString();
        //SendToClient(JsonUtility.ToJson(m), c);
        ////m.player.cubeColor.r = 5;
        ////m.player.cubeColor.g = 5;
        ////m.player.cubeColor.b = 5;
    }
    void UpdatePlayers()
    {
        //Since we're using a list to store the data(pos, id, color) we add the updated date from each client and send it back to everyone in he server
        //Figuring out how to to add and remove was new to me because I didn't have much experience with lists
        //At first I tried using an array and... found out Arrays actually suck atleast for this because resizing isn't really a thing

        for (int i = 0; i < m_Connections.Length; i++)
        {
            playerUpdateInfo.GameUpdate.Insert(i, playerList.GameUpdate[i]);
            playerUpdateInfo.GameUpdate.RemoveAt(i + 1);

            SendToClient(JsonUtility.ToJson(playerUpdateInfo), m_Connections[i]);
        }
    }
    //Wasn't reall able to figure out the disconnect implementation
    void OnDisconnect(int i){
        Debug.Log("Client disconnected from server");
        m_Connections[i] = default(NetworkConnection);
    }

    void Update ()
    {
        m_Driver.ScheduleUpdate().Complete();

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {

                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c  != default(NetworkConnection))
        {            
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }


        // Read Incoming Messages
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);

            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            while (cmd != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
             
        }
        UpdatePlayers();
    }
}