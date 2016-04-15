/* Code by Lacey Taylor and James Bowden
*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceModel.Web;
using static System.Net.HttpStatusCode;

namespace Boggle
{
    public class BoggleService : IBoggleService
    {

        private static string BoggleDB;

        static BoggleService()
        {
            BoggleDB = ConfigurationManager.ConnectionStrings["BoggleDB"].ConnectionString;
        }

        private static void SetStatus(HttpStatusCode status)
        {
            WebOperationContext.Current.OutgoingResponse.StatusCode = status;
        }

        public Stream API()
        {
            SetStatus(OK);
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";
            return File.OpenRead(AppDomain.CurrentDomain.BaseDirectory + "index.html");
        }

        public UserTokenObject CreateUser(NicknameObject name)
        {
            if (name.Nickname == null || name.Nickname.Trim() == "")
            {
                SetStatus(Forbidden);
                return null;
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(BoggleDB))
                {
                    conn.Open();

                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        using (SqlCommand command = new SqlCommand
                                ("insert into Users(UserID,Nickname) values (@UserID, @Nickname)",
                                conn, trans)
                              )
                        {
                            string userID = Guid.NewGuid().ToString();
                            command.Parameters.AddWithValue("@UserID", userID);
                            command.Parameters.AddWithValue("@Nickname", name.Nickname.Trim());

                            command.ExecuteNonQuery();
                            SetStatus(Created);

                            trans.Commit();

                            UserTokenObject temp = new UserTokenObject();
                            temp.UserToken = userID;

                            return temp;

                        }
                    }

                }
            }
        }

        public GameIDObject JoinGame(JoinGameInput input)
        {
            string gameid = "";
            string buildQuery = "";
            bool pending = false;
            BoggleBoard bog = new BoggleBoard();
            int totaledTime = 0;
            int pendingTime = 0;
            int pendingGameID = 0;

            if (input.UserToken == null || input.UserToken.Trim() == "")
            {
                SetStatus(Forbidden);
                return null;
            }

            if (input.TimeLimit < 5 || input.TimeLimit > 120)
            {
                SetStatus(Forbidden);
                return null;
            }

            using (SqlConnection conn = new SqlConnection(BoggleDB))
            {
                conn.Open();

                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("select * from Games where Player2 is NULL", conn, trans))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {

                                buildQuery = "insert into GAMES(Player1, TimeLimit) output inserted.GameID values (@UserID, @TimeLimit)";
                                pending = true;
                            }
                            else
                            {

                                while (reader.Read())
                                {
                                    pendingTime = (int)reader["TimeLimit"];
                                    pendingGameID = (int)reader["GameID"];
                                }

                                totaledTime = (pendingTime + input.TimeLimit) / 2;

                                buildQuery = "update Games set Player2 = @UserID, Board = @Board, TimeLimit = @NewTime, StartTime = CURRENT_TIMESTAMP output inserted.GameID where GameID = @pendingGameID";

                                pending = false;
                            }

                        }
                    }

                    if (!pending)
                    {
                        using (SqlCommand command = new SqlCommand(buildQuery, conn, trans))
                        {
                            command.Parameters.AddWithValue("@UserID", input.UserToken);
                            command.Parameters.AddWithValue("@Board", bog.ToString());
                            command.Parameters.AddWithValue("@NewTime", totaledTime);
                            command.Parameters.AddWithValue("@StartTime", input.TimeLimit);
                            command.Parameters.AddWithValue("@pendingGameID", pendingGameID);

                            gameid = command.ExecuteScalar().ToString();
                            SetStatus(Created);

                            trans.Commit();

                        }
                    }
                    else
                    {
                        using (SqlCommand command = new SqlCommand(buildQuery, conn, trans))
                        {
                            command.Parameters.AddWithValue("@UserID", input.UserToken);
                            command.Parameters.AddWithValue("@TimeLimit", input.TimeLimit);

                            gameid = command.ExecuteScalar().ToString();
                            SetStatus(Accepted);

                            trans.Commit();
                        }
                    }
                }
            }

            GameIDObject temp = new GameIDObject();
            temp.GameID = gameid;
            return temp;
        }


        public void CancelJoin(UserTokenObject token)
        {
            int Gameid = 0;
            if (token.UserToken == null || token.UserToken.Trim() == "")
            {
                SetStatus(Forbidden);
            }

            using (SqlConnection conn = new SqlConnection(BoggleDB))
            {
                conn.Open();

                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("select * from Games where Player1=@UserToken and Player2 is NULL", conn, trans))
                    {
                        command.Parameters.AddWithValue("@UserToken", token.UserToken);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                SetStatus(Forbidden);
                            }
                            else
                            {
                                while (reader.Read())
                                {
                                    Gameid = (int)reader["GameID"];
                                }
                            }
                        }
                    }

                    using (SqlCommand command = new SqlCommand("delete from Games where GameID = @Gameid", conn, trans))
                    {
                        command.Parameters.AddWithValue("@Gameid", Gameid);
                        command.ExecuteNonQuery();
                    }

                    trans.Commit();
                }
            }
        }


        public GameStatus GetStatus(string GameID, string bs)
        {
            GameStatus stat = new GameStatus();
            string hold1 = "", hold2 = "";
            int scoreCounter = 0;
            List<WordObject> wordList = new List<WordObject>();

            if (GameID == null || GameID.Trim() == "")
            {
                SetStatus(Forbidden);
                return null;
            }

            using (SqlConnection conn = new SqlConnection(BoggleDB))
            {
                conn.Open();

                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("select * from Games where GameID=@GameID", conn, trans))
                    {
                        command.Parameters.AddWithValue("@GameID", Int32.Parse(GameID));

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                SetStatus(Forbidden);
                                return null;
                            }
                            else
                            {
                                while (reader.Read())
                                {
                                    stat.GameID = reader["GameID"].ToString();
                                    hold1 = (string)reader["Player1"];
                                    hold2 = (string)reader["Player2"];
                                    stat.Board = (string)reader["Board"];
                                    stat.TimeLeft = (int)reader["TimeLimit"];
                                    stat.InitalTime = (DateTime)reader["StartTime"];
                                }

                                if (stat.Player2 == null)
                                {
                                    stat.GameState = "pending";
                                    SetStatus(OK);

                                    GameStatus tempPend = new GameStatus();
                                    tempPend.GameState = "pending";
                                    return tempPend;

                                }

                                DateTime tempTime = stat.InitalTime.AddSeconds(stat.TimeLeft);

                                if (tempTime >= DateTime.Now)
                                {

                                    stat.GameState = "completed";
                                    SetStatus(OK);
                                }
                                else
                                {
                                    stat.GameState = "active";
                                }
                            }
                        }
                    }

                    using (SqlCommand command = new SqlCommand("select * from Words where GameID=@GameID and Player=@Player", conn, trans))
                    {
                        command.Parameters.AddWithValue("@GameID", Int32.Parse(GameID));
                        command.Parameters.AddWithValue("@Player", stat.Player1);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                WordObject worb = new WordObject();
                                worb.Word = (string)reader["Word"];
                                worb.Score = (int)reader["Score"];

                                wordList.Add(worb);
                                scoreCounter += worb.Score;
                            }
                        }

                        stat.Player1WordList = new List<WordObject>(wordList);
                    }

                    using (SqlCommand command = new SqlCommand("select Nickname from Users where UserID=@UserID", conn, trans))
                    {
                        command.Parameters.AddWithValue("@UserID", hold1);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            PlayerObject p1 = new PlayerObject();
                            while (reader.Read())
                            {
                                p1.Nickname = ((string)reader["Nickname"]);
                                p1.Score = scoreCounter;
                            }

                            stat.Player1 = p1;
                        }
                    }

                    wordList.Clear();
                    scoreCounter = 0;


                    using (SqlCommand command = new SqlCommand("select * from Words where GameID=@GameID and Player=@Player", conn, trans))
                    {
                        command.Parameters.AddWithValue("@GameID", Int32.Parse(GameID));
                        command.Parameters.AddWithValue("@Player", stat.Player2);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                WordObject worb = new WordObject();
                                worb.Word = (string)reader["Word"];
                                worb.Score = (int)reader["Score"];

                                wordList.Add(worb);
                                scoreCounter += worb.Score;
                            }
                        }

                        stat.Player2WordList = new List<WordObject>(wordList);
                    }

                    using (SqlCommand command = new SqlCommand("select Nickname from Users where UserID=@UserID", conn, trans))
                    {
                        command.Parameters.AddWithValue("@UserID", hold1);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            PlayerObject p2 = new PlayerObject();
                            while (reader.Read())
                            {

                                p2.Nickname = ((string)reader["Nickname"]);
                                p2.Score = scoreCounter;
                            }
                            stat.Player2 = p2;
                        }
                    }

                    trans.Commit();


                    if (stat.GameState == "completed")
                    {
                        GameStatus tempStat = new GameStatus();
                        tempStat.GameState = "completed";
                        tempStat.Board = stat.Board;
                        tempStat.InitalTime = stat.InitalTime;
                        tempStat.TimeLeft = 0;
                        tempStat.Player1 = stat.Player1;
                        tempStat.Player1WordList = stat.Player1WordList;
                        tempStat.Player2 = stat.Player1;
                        tempStat.Player2WordList = stat.Player1WordList;

                        return tempStat;

                    }
                    else
                    {
                        GameStatus tempStat = new GameStatus();
                        tempStat.GameState = "active";
                        tempStat.Board = stat.Board;
                        tempStat.InitalTime = stat.InitalTime;
                        tempStat.TimeLeft = stat.TimeLeft;
                        tempStat.Player1 = stat.Player1;
                        tempStat.Player2 = stat.Player1;

                        return tempStat;
                    }
                }
            }
        }


        public ScoreObject PlayWord(PlayWordInput input, string GameID)
        {

            ScoreObject temp = new ScoreObject();


            if (GameID == null || GameID.Trim() == "")
            {
                SetStatus(Forbidden);
                return null;
            }

            if (input.Word == null || input.Word.Trim() == "")
            {
                SetStatus(Forbidden);
                temp.Score = 0;
                return temp;
            }

            using (SqlConnection conn = new SqlConnection(BoggleDB))
            {
                conn.Open();
                string theBoard = "";
                int wordValue = 0;
                BoggleBoard brd;

                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("select * from Games where GameID = @GameID", conn, trans))
                    {
                        command.Parameters.AddWithValue("@GameID", Int32.Parse(GameID));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                theBoard = (string)reader["Board"];
                            }
                        }

                        brd = new BoggleBoard(theBoard);

                        if (brd.CanBeFormed(input.Word))
                        {

                            wordValue = GetValue(input.Word.Trim());
                        }
                    }

                    using (SqlCommand command = new SqlCommand("insert into Words(Word,GameID,Player,Score) values (@Word,@GameID,@Player,@Score)", conn, trans))
                    {
                        command.Parameters.AddWithValue("@Word", input.Word);
                        command.Parameters.AddWithValue("@GameID", Int32.Parse(GameID));
                        command.Parameters.AddWithValue("@Player", input.UserToken);
                        command.Parameters.AddWithValue("@Score", wordValue);

                        command.ExecuteNonQuery();

                    }
                    temp.Score = wordValue;

                    trans.Commit();
                    return temp;
                }
            }
        }




        //public ScoreObject PlayWord(PlayWordInput input, string GameID)
        //{
        //    ScoreObject temp = new ScoreObject();
        //    lock (sync)
        //    {
        //        if (input.Word == null || input.Word.Trim() == "")
        //        {
        //            SetStatus(Forbidden);
        //            temp.Score = 0;
        //            return temp;
        //        }
        //        else
        //        {
        //            input.Word = input.Word.Trim();
        //            input.Word = input.Word.ToUpper();

        //            int gameID;
        //            Int32.TryParse(GameID, out gameID);
        //            TheGame game = gameDict[gameID];

        //            if (!(game.GameState == "pending"))
        //            {
        //                if (game.Timer.ElapsedMilliseconds > (game.InitalTime * 1000))
        //                {
        //                    game.GameState = "completed";
        //                }
        //            }
        //            if (game.GameState != "active")
        //            {
        //                SetStatus(Conflict);
        //                temp.Score = 0;
        //                return temp;
        //            }

        //            if (game.Board.CanBeFormed(input.Word))
        //            {
        //                int wordValue;
        //                wordValue = GetValue(input.Word.Trim());

        //                if (game.Player1UserToken == input.UserToken)
        //                {
        //                    if (game.Player1WordList.Contains(input.Word.Trim()))
        //                    {
        //                        SetStatus(OK);
        //                        temp.Score = 0;
        //                        return temp;
        //                    }
        //                    game.Player1WordList.Add(input.Word.Trim());
        //                    game.Player1Score += wordValue;
        //                    temp.Score = wordValue;
        //                    return temp;
        //                }
        //                else
        //                {
        //                    if (game.Player2WordList.Contains(input.Word.Trim()))
        //                    {
        //                        SetStatus(OK);
        //                        temp.Score = 0;
        //                        return temp;
        //                    }
        //                    game.Player2WordList.Add(input.Word.Trim());
        //                    game.Player2Score += wordValue;
        //                    temp.Score = wordValue;
        //                    return temp;

        //                }

        //            }
        //            else
        //            {
        //                SetStatus(OK);
        //                temp.Score = 0;
        //                return temp;
        //            }

        //        }
        //    }

        //}

        //public GameStatus GetStatus(string GameID, string bS)
        //{
        //    int gameID;
        //    int timeLeft = 0;
        //    GameStatus stat = new GameStatus();
        //    Int32.TryParse(GameID, out gameID);
        //    lock (sync)
        //    {
        //        if (gameDict.ContainsKey(gameID))
        //        {
        //            SetStatus(OK);
        //            TheGame game = gameDict[gameID];

        //            if (game.GameState == "pending")
        //            {
        //                stat.GameState = "pending";
        //                return stat;
        //            }
        //            if (game.Timer.ElapsedMilliseconds > game.InitalTime * 1000)
        //            {
        //                game.GameState = "completed";
        //            }

        //            if (bS != null)
        //            {
        //                if (bS == "yes")
        //                {
        //                    stat.GameState = game.GameState;

        //                    if (game.GameState == "completed")
        //                    {
        //                        timeLeft = 0;
        //                    }
        //                    else
        //                    {
        //                        timeLeft = (int)((game.InitalTime * 1000) - (game.Timer.ElapsedMilliseconds) / 1000);
        //                    }


        //                    stat.TimeLeft = timeLeft;
        //                    stat.Player1 = new PlayerObject();
        //                    stat.Player2 = new PlayerObject();
        //                    stat.Player1.Score = game.Player1Score;
        //                    stat.Player2.Score = game.Player2Score;

        //                    return stat;
        //                }
        //            }

        //            if (game.GameState == "active")
        //            {
        //                timeLeft = (int)((game.InitalTime * 1000) - (game.Timer.ElapsedMilliseconds) / 1000);

        //                stat.GameState = "active";
        //                stat.Board = game.Board.ToString();
        //                stat.InitalTime = game.InitalTime;

        //                stat.TimeLeft = timeLeft;
        //                stat.Player1 = new PlayerObject();
        //                stat.Player2 = new PlayerObject();
        //                stat.Player1.Score = game.Player1Score;
        //                stat.Player2.Score = game.Player2Score;

        //                return stat;
        //            }
        //            else
        //            {
        //                stat.GameState = "completed";
        //                stat.Board = game.Board.ToString();
        //                stat.InitalTime = game.InitalTime;
        //                stat.TimeLeft = timeLeft;

        //                stat.Player1 = new PlayerObject();
        //                stat.Player2 = new PlayerObject();

        //                stat.Player1.Nickname = game.Player1;
        //                stat.Player2.Nickname = game.Player2;

        //                stat.Player1.Score = game.Player1Score;
        //                stat.Player2.Score = game.Player2Score;

        //                foreach (var item in game.Player1WordList)
        //                {
        //                    WordObject wordToAdd = new WordObject();
        //                    wordToAdd.Word = item;
        //                    wordToAdd.Score = GetValue(item);
        //                    stat.Player1WordList.Add(wordToAdd);
        //                }

        //                foreach (var item in game.Player2WordList)
        //                {
        //                    WordObject wordToAdd = new WordObject();
        //                    wordToAdd.Word = item;
        //                    wordToAdd.Score = GetValue(item);
        //                    stat.Player2WordList.Add(wordToAdd);
        //                }
        //            }

        //            return stat;

        //        }
        //        else
        //        {
        //            SetStatus(Forbidden);
        //            return null;
        //        }
        //    }
        //}

        private int GetValue(string word)
        {
            string[] dictArra;
            string dictionary = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "\\dictionary.txt");
            dictArra = dictionary.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            if (Array.BinarySearch(dictArra, word) < 0)
            {
                return -1;
            }
            else
            {
                switch (word.Length)
                {
                    case 1:
                    case 2:
                        return 0;
                    case 3:
                    case 4:
                        return 1;
                    case 5:
                        return 2;
                    case 6:
                        return 3;
                    case 7:
                        return 5;
                    default:
                        return 11;
                }
            }
        }

        //private void MakeBoard(TheGame game)
        //{
        //    BoggleBoard board = new BoggleBoard();
        //    game.Board = board;
        //}
    }

    public class GameStatus
    {
        public string GameState { get; set; }
        public int TimeLeft { get; set; }
        public PlayerObject Player1 { get; set; }
        public PlayerObject Player2 { get; set; }

        public string GameID { get; set; }
        public DateTime InitalTime { get; set; }
        public string Board { get; set; }
        public Stopwatch Timer { get; set; }

        public string Player1UserToken { get; set; }
        public int Player1Score { get; set; }
        public int Player1MaxTime { get; set; }
        public List<WordObject> Player1WordList;

        public string Player2UserToken { get; set; }
        public int Player2Score { get; set; }
        public int Player2MaxTime { get; set; }
        public List<WordObject> Player2WordList;
    }

    public class PlayerObject
    {
        public int Score { get; set; }
        public string Nickname { get; set; }
    }

    public class PlayWordInput
    {
        public string Word { get; set; }
        public String UserToken { get; set; }
        public String GameID { get; set; }
    }

    public class ScoreObject
    {
        public int Score { get; set; }
    }

    public class TheGame
    {
        public string GameID { get; set; }
        public string GameState { get; set; }
        public int TimeLeft { get; set; }
        public DateTime InitalTime { get; set; }
        public string Board { get; set; }
        public Stopwatch Timer { get; set; }

        public string Player1 { get; set; }
        public string Player1UserToken { get; set; }
        public int Player1Score { get; set; }
        public int Player1MaxTime { get; set; }
        public List<WordObject> Player1WordList;

        public string Player2 { get; set; }
        public string Player2UserToken { get; set; }
        public int Player2Score { get; set; }
        public int Player2MaxTime { get; set; }
        public List<WordObject> Player2WordList;




    }

    public class JoinGameInput
    {
        public string UserToken { get; set; }
        public int TimeLimit { get; set; }
    }

    public class GameIDObject
    {
        public string GameID { get; set; }
    }

    public class NicknameObject
    {
        public string Nickname { get; set; }
    }
    public class UserTokenObject
    {
        public string UserToken { get; set; }
    }

    public class WordObject
    {
        public string Word { get; set; }
        public int Score { get; set; }
    }
}
