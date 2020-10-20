using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkMessages
{
    public enum Commands{
        PLAYER_UPDATE,
        SERVER_UPDATE,
        HANDSHAKE,
        PLAYER_INPUT,
        CONNECTION_ACCEPTED,
        GAME_UPDATE,

    }

    [System.Serializable]
    public class NetworkHeader{
        public Commands cmd;
    }
    [System.Serializable]
    public class HandshakeMsg:NetworkHeader{
        public List<NetworkObjects.NetworkPlayer> player;
        public HandshakeMsg(){      // Constructor
            cmd = Commands.HANDSHAKE;
            player = new List<NetworkObjects.NetworkPlayer>();
        }
    }
    //Added this to update your players position
    [System.Serializable]
    public class ConnectionAcceptedMsg : NetworkHeader
    {
        public List<NetworkObjects.NetworkPlayer> player;
        public ConnectionAcceptedMsg()
        {      // Constructor
            cmd = Commands.CONNECTION_ACCEPTED;
            player = new List<NetworkObjects.NetworkPlayer>();
        }
    }
    [System.Serializable]
    public class GameUpdateMsg : NetworkHeader
    {
        public List<NetworkObjects.NetworkPlayer> GameUpdate;
        public GameUpdateMsg()
        {      // Constructor
            cmd = Commands.GAME_UPDATE;
            GameUpdate = new List<NetworkObjects.NetworkPlayer>();
        }
    };

    [System.Serializable]
    public class PlayerUpdateMsg:NetworkHeader{
        public NetworkObjects.NetworkPlayer player;
        public PlayerUpdateMsg(){      // Constructor
            cmd = Commands.PLAYER_UPDATE;
            player = new NetworkObjects.NetworkPlayer();
        }
    };

    public class PlayerInputMsg:NetworkHeader{
        public Input myInput;
        public PlayerInputMsg(){
            cmd = Commands.PLAYER_INPUT;
             myInput = new Input();
        }
    }
    [System.Serializable]
    public class  ServerUpdateMsg:NetworkHeader{
        public List<NetworkObjects.NetworkPlayer> players;
        public ServerUpdateMsg(){      // Constructor
            cmd = Commands.SERVER_UPDATE;
            players = new List<NetworkObjects.NetworkPlayer>();
        }
    }
}

namespace NetworkObjects
{ 
    //Easier to have them all in one
    //Wasn't feeling the cube changing colour hope thats okay
    [System.Serializable]
    public class NetworkPlayer
    {
        public string id;
        //public Color cubeColor;
        public Vector3 cubPos;

        public NetworkPlayer()
        {
            //cubeColor = new Color();
            cubPos = new Vector3();
        }
    }
}


