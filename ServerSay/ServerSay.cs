/// Based on ServerSay sample by https://www.patreon.com/Xango2000

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

//using statement for API2 to work. Requires Reference to ModApi.DLL (see solution Explorer on the right)
using Eleon;

//using statement for API1 to work. Requires Refence to Mif.DLL (see solution Explorer on the right)
using Eleon.Modding;

namespace ServerSay
{
    public class ServerSay : ModInterface
    {
        /// <summary> Max range of sequence number so that 100 of that transaction type can flow through. </summary>
        const ushort SeqNumRangeMax = 99;

        //Some way to track the last Sequence Number you used, note that ushort has a limit of numbers
        ushort NewSequenceNumber = 0;

        const ushort AddPlayerSeqNum = 8192;
        ushort addPlayerAcc = 0;

        const ushort SubPlayerSeqNum = 8292;
        ushort subPlayerAcc = 0;

        const ushort AddCreditsSeqNum = 8392;
        ushort addCreditsAcc = 0;

        const ushort SubCredtisSeqNum = 8493;
        ushort subCreditsAcc = 0;

        //These 2 variables so you can access the API functions later
        internal static ModGameAPI modApi1;

        //Current list of active players
        readonly Dictionary<string, int> PlayerList = new Dictionary<string, int> { };

        //This variable is to hold the AdminConfig data
        internal static AdminConfig.Root AdminConfigData = new AdminConfig.Root { };

        //To store SeqNr so you can call up all the data in your chain
        readonly Dictionary<ushort, StorableData> SeqNrStorage = new Dictionary<ushort, StorableData> { };

        //A Class to store all the received data for when you have to chain requests to get all the data you need
        internal class StorableData
        {

            //TriggeringPlayer: I use for storing PlayerIDs
            public int TriggeringPlayer;

            //ChatInfo stores all of the ChatInfo from when a player said something that triggered the mod
            public ChatInfo ChatInfo;
        }

        //#######################################################################################################################################################
        //############################################################ API1 Starts Here #########################################################################
        //#######################################################################################################################################################


        //Required Function for API1
        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {

            //"If we received a chat message"
            if (eventId == CmdId.Event_ChatMessage)
            {

                //get the ChatInfo to a point where we can access it
                ChatInfo Received_ChatInfo = (ChatInfo)data;

                //"if message startswith /say"
                if (Received_ChatInfo.msg.StartsWith("/say "))
                {

                    //create a StorableData because we are going to need to request PlayerInfo to see if the player is an Admin
                    StorableData newStorable = new StorableData
                    {
                        TriggeringPlayer = Received_ChatInfo.playerId,
                        ChatInfo = Received_ChatInfo
                    };

                    //Increment the NewSequenceNumber variable so we get an unused number
                    NewSequenceNumber++;

                    //Store the StoreableData as the NewSequenceNumber so we have it for later
                    SeqNrStorage[NewSequenceNumber] = newStorable;

                    //API1 request the PlayerInfo for the TriggeringPlayer using the NewSequenceNumber so we can recall the ChatInfo later
                    modApi1.Game_Request(CmdId.Request_Player_Info, (ushort)NewSequenceNumber, new Id(Received_ChatInfo.playerId));
                }
                else if (Received_ChatInfo.msg.StartsWith("/bank "))
                {
                    StorableData bankArgs = new StorableData
                    {
                        TriggeringPlayer = Received_ChatInfo.playerId,
                        ChatInfo = Received_ChatInfo
                    };

                    ushort token = RotateSeqNumPtr(ref addCreditsAcc, AddCreditsSeqNum);

                    var chatArgs = bankArgs.ChatInfo.msg.Split(' ');
                    string recipient = chatArgs[1];
                    int.TryParse(chatArgs[2], out int amount);

                    if (recipient.Equals("save")) {

                    } else if (!PlayerList.ContainsKey(recipient))
                    {
                        modApi1.Console_Write($"Failed transfer: player \"{recipient}\" not found in player list");
                    } else if (modApi1.Game_Request(CmdId.Request_Player_AddCredits, token, new IdCredits(PlayerList[recipient], amount))) {
                        SeqNrStorage[token] = bankArgs;
                        modApi1.Game_Request(CmdId.Request_Player_Info, token, new Id(bankArgs.TriggeringPlayer));
                    }
                }
            }

            //"If we receive PlayerInfo"
            else if (eventId == CmdId.Event_Player_Info)
            {
                //get the PlayerInfo to a point where we can access it
                PlayerInfo Received_PlayerInfo = (PlayerInfo)data;

                if (seqNr >= AddPlayerSeqNum || seqNr <= AddPlayerSeqNum + SeqNumRangeMax)
                {
                    PlayerList.Add(Received_PlayerInfo.playerName, Received_PlayerInfo.entityId);
                    SeqNrStorage.Remove(seqNr);
                }
                else if (seqNr >= SubPlayerSeqNum || seqNr <= SubPlayerSeqNum + SeqNumRangeMax)
                {
                    PlayerList.Remove(Received_PlayerInfo.playerName);
                    SeqNrStorage.Remove(seqNr);
                }
                else if (seqNr >= AddCreditsSeqNum || seqNr <= AddCreditsSeqNum + SeqNumRangeMax)
                {
                    StorableData bankArgs = SeqNrStorage[seqNr];

                    ushort token = RotateSeqNumPtr(ref subCreditsAcc, SubCredtisSeqNum);

                    var chatArgs = bankArgs.ChatInfo.msg.Split(' ');
                    string recipient = chatArgs[1];
                    int.TryParse(chatArgs[2], out int amount);

                    if (Received_PlayerInfo.credits < amount)
                    {
                        modApi1.Game_Request(CmdId.Request_Player_AddCredits, token, new IdCredits(PlayerList[recipient], -amount));
                        modApi1.Console_Write($"Failed transfer: insufficent credits");
                    }

                    if (modApi1.Game_Request(CmdId.Request_Player_AddCredits, token, new IdCredits(Received_PlayerInfo.entityId, -amount)))
                    {
                        modApi1.Console_Write($"Succesfully transferred {amount} Credits to {recipient}");
                    } else {
                        // TODO: Reason codes, like insufficent funds, player not found?
                        modApi1.Game_Request(CmdId.Request_Player_AddCredits, token, new IdCredits(PlayerList[recipient], -amount));
                        modApi1.Console_Write($"Failed transfer: unkown reason");
                    }
                }
                //"if SeqNrStorage contains the SeqNr we just received..."
                else if (SeqNrStorage.ContainsKey(seqNr))
                {

                    //Retrieve the data we stored under that SeqNr
                    StorableData RetrievedData = SeqNrStorage[seqNr];

                    //"if the PlayerInfo we received matches the player that triggered the mod AND they are not a regular player
                    //permission 3 is GM, 6 is Moderator, 9 is Admin, Player is 0 (I think)
                    if (SeqNrStorage[seqNr].TriggeringPlayer == Received_PlayerInfo.entityId && Received_PlayerInfo.permission > 1)
                    {

                        //Try statement in case we cause an error here
                        try
                        {

                            //we already retrieved the data so we do not need to keep storing it, lets free upt he SeqNr for later
                            SeqNrStorage.Remove(seqNr);

                            //Split the chat message on Spaces
                            List<string> Restring = new List<string>(RetrievedData.ChatInfo.msg.Split(' '));

                            //remove the "/say"
                            Restring.Remove(Restring[0]);

                            //put the chat message back together
                            string Message = string.Join(" ", Restring.ToArray());

                            //we are going to be using a "Telnet" command for this next part, format the string for that purpose
                            string ConsoleCommand = "say '" + Message + "'";

                            //it says "Console Command" but its really a "Telnet" command, we dont need to store this SeqNr for later. This line is sending the request for the server to repeat what the Admin said after /say
                            modApi1.Game_Request(CmdId.Request_ConsoleCommand, (ushort)NewSequenceNumber, new PString(ConsoleCommand));
                        }
                        //The other part of the Try statement, it catches errors so you can log them.
                        catch { }
                    }
                }
            }
            else if (eventId == CmdId.Event_Player_Connected)
            {
                var mod = RotateSeqNumPtr(ref addPlayerAcc, AddPlayerSeqNum);
                modApi1.Game_Request(CmdId.Request_Player_Info, (ushort) (AddPlayerSeqNum+mod), new Id((int)data));
            }
            else if (eventId == CmdId.Event_Player_Disconnected)
            {
                var mod = RotateSeqNumPtr(ref subPlayerAcc, SubPlayerSeqNum);
                modApi1.Game_Request(CmdId.Request_Player_Info, (ushort) (SubPlayerSeqNum + mod), new Id((int)data));
            }
        }

        private ushort RotateSeqNumPtr(ref ushort seqNum, ushort constSeqRef) 
            => seqNum == constSeqRef+SeqNumRangeMax ? (ushort) 0 : seqNum++;

        //required by API1, though we arent using it for anything in this mod
        public void Game_Exit()
        {
            //API1 version of Shutdown
        }

        //required by API1
        public void Game_Start(ModGameAPI dediAPI)
        {
            //API1 version of Init

            //store the ModGameAPI as modApi1 so we can use those functions later
            modApi1 = dediAPI;
        }

        //required by API1, though we arent using it for anything in this mod. In fact, I think Eleon broke this one...
        public void Game_Update()
        {
            //API1 version of Application_Update (not shown here)
        }
    }
}
