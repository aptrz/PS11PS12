using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using Boggle;
using System.Dynamic;
using Newtonsoft.Json;
using static System.Net.HttpStatusCode;
using System.Threading.Tasks;
using System.Text;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ServerGrader
{
    /// <summary>
    /// NOTE:  The service must already be running elsewhere, such as in a separate Visual Studio
    /// or on a remote server, before these tests are run.  When the tests are started, the pending
    /// game should contain NO players.
    /// 
    /// For best results, run these tests against a server to which you have exlusive access.
    /// Othewise, competing users may interfere with the tests.
    /// </summary>
    [TestClass]
    public class Tests
    {
        /// <summary>
        /// Creates an HttpClient for communicating with the boggle server.
        /// </summary>
        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:60000");
            //client.BaseAddress = new Uri("http://bogglecs3500s16db.azurewebsites.net");
            return client;
        }

        /// <summary>
        /// Helper for serializaing JSON.
        /// </summary>
        private static StringContent Serialize(dynamic json)
        {
            return new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json");
        }

        /// <summary>
        /// Given a board configuration, returns all the valid words.
        /// </summary>
        private static IList<string> AllValidWords(string board)
        {
            ISet<string> dictionary = new HashSet<string>();
            using (StreamReader words = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + @"/dictionary.txt"))
            {
                string word;
                while ((word = words.ReadLine()) != null)
                {
                    dictionary.Add(word.ToUpper());
                }
            }
            BoggleBoard bb = new BoggleBoard(board);
            List<string> validWords = new List<string>();
            foreach (string word in dictionary)
            {
                if (bb.CanBeFormed(word))
                {
                    validWords.Add(word);
                }
            }
            return validWords;
        }

        /// <summary>
        /// Returns the score for a word.
        /// </summary>
        private static int GetScore(string word)
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

        /// <summary>
        /// Makes a user and asserts that the resulting status code is equal to the
        /// status parameter.  Returns a Task that will produce the new userID.
        /// </summary>
        private async Task<string> MakeUser(String nickname, HttpStatusCode status)
        {
            dynamic name = new ExpandoObject();
            name.Nickname = nickname;

            using (HttpClient client = CreateClient())
            {
                HttpResponseMessage response = await client.PostAsync("/BoggleService.svc/users", Serialize(name));
                Assert.AreEqual(status, response.StatusCode);
                if (response.IsSuccessStatusCode)
                {
                    String result = await response.Content.ReadAsStringAsync();
                    dynamic user = JsonConvert.DeserializeObject(result);
                    Assert.IsNotNull(user.UserToken);
                    return user.UserToken;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Joins the game and asserts that the resulting status code is equal to the parameter status.
        /// Returns a Task that will produce the new GameID.
        /// </summary>
        private async Task<string> JoinGame(String player, int timeLimit, HttpStatusCode status)
        {
            dynamic user = new ExpandoObject();
            user.UserToken = player;
            user.TimeLimit = timeLimit;

            using (HttpClient client = CreateClient())
            {
                HttpResponseMessage response = await client.PostAsync("/BoggleService.svc/games", Serialize(user));
                Assert.AreEqual(status, response.StatusCode);
                if (response.IsSuccessStatusCode)
                {
                    String result = await response.Content.ReadAsStringAsync();
                    dynamic game = JsonConvert.DeserializeObject(result);
                    Assert.IsNotNull(game.GameID);
                    return game.GameID;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Cancels the pending game and asserts that the resulting status code is
        /// equal to the parameter status.
        /// </summary>
        private async Task CancelGame(String player, HttpStatusCode status)
        {
            dynamic user = new ExpandoObject();
            user.UserToken = player;

            using (HttpClient client = CreateClient())
            {
                HttpResponseMessage response = await client.PutAsync("/BoggleService.svc/games", Serialize(user));
                Assert.AreEqual(status, response.StatusCode);
            }
        }

        /// <summary>
        /// Gets the status for the specified game and value of brief.  Asserts that the resulting
        /// status code is equal to the parameter status.  Returns a task that produces the object
        /// returned by the service.
        /// </summary>
        private async Task<dynamic> GetStatus(String game, string brief, HttpStatusCode status)
        {
            using (HttpClient client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync("/BoggleService.svc/games/" + game + "?brief=" + brief);
                Assert.AreEqual(status, response.StatusCode);
                if (response.IsSuccessStatusCode)
                {
                    String result = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject(result);
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Plays a word and asserts that the resulting status code is equal to the parameter
        /// status.  Returns a task that will produce the score of the word.
        /// </summary>
        private async Task<int> PlayWord(String player, String game, String word, HttpStatusCode status)
        {
            dynamic play = new ExpandoObject();
            play.UserToken = player;
            play.Word = word;

            using (HttpClient client = CreateClient())
            {
                HttpResponseMessage response = await client.PutAsync("/BoggleService.svc/games/" + game, Serialize(play));
                Assert.AreEqual(status, response.StatusCode);
                if (response.IsSuccessStatusCode)
                {
                    String result = await response.Content.ReadAsStringAsync();
                    dynamic score = JsonConvert.DeserializeObject(result);
                    return score.Score;
                }
                else
                {
                    return -2;
                }
            }
        }

        /// <summary>
        /// Try to make a user.
        /// </summary>
        [TestMethod]
        public void TestMakeUser()
        {
            MakeUser(null, Forbidden).Wait();
            MakeUser("  ", Forbidden).Wait();
            MakeUser("Player", Created).Wait();
        }

        /// <summary>
        /// Try to make a game.
        /// </summary>
        [TestMethod]
        public void TestJoinGame()
        {
            JoinGame("hello", 10, Forbidden).Wait();
            JoinGame("hello", 1, Forbidden).Wait();
            String player1 = MakeUser("Player 1", Created).Result;
            String player2 = MakeUser("Player 2", Created).Result;
            JoinGame(player1, 4, Forbidden).Wait();
            JoinGame(player1, 121, Forbidden).Wait();

            String game1 = JoinGame(player1, 10, Accepted).Result;
            JoinGame(player1, 10, Conflict).Wait();
            String game2 = JoinGame(player2, 10, Created).Result;
            Assert.AreEqual(game1, game2);

            String player3 = MakeUser("Player 3", Created).Result;
            String player4 = MakeUser("Player 4", Created).Result;
            String game3 = JoinGame(player3, 10, Accepted).Result;
            JoinGame(player3, 10, Conflict).Wait();
            String game4 = JoinGame(player4, 10, Created).Result;
            Assert.AreEqual(game3, game4);

            Assert.AreNotEqual(game1, game3);
        }

        /// <summary>
        /// Test canceling a game.
        /// </summary>
        [TestMethod]
        public void TestCancelGame()
        {
            String player1 = MakeUser("Player 1", Created).Result;
            String player2 = MakeUser("Player 2", Created).Result;
            String game1 = JoinGame(player1, 10, Accepted).Result;
            CancelGame(null, Forbidden).Wait();
            CancelGame("  ", Forbidden).Wait();
            CancelGame(player2, Forbidden).Wait();
            CancelGame(player1, OK).Wait();
            CancelGame(player1, Forbidden).Wait();
            String game2 = JoinGame(player1, 10, Accepted).Result;
            String game3 = JoinGame(player2, 10, Created).Result;
            Assert.AreEqual(game2, game3);
        }

        /// <summary>
        /// Test getting status
        /// </summary>
        [TestMethod]
        public void TestStatus1()
        {
            String player1 = MakeUser("Player 1", Created).Result;
            String player2 = MakeUser("Player 2", Created).Result;
            String game1 = JoinGame(player1, 10, Accepted).Result;
            GetStatus(game1, "no", OK).Wait();
            String game2 = JoinGame(player2, 20, Created).Result;
            GetStatus(game1, "no", OK).Wait();

            GetStatus("blank", "no", Forbidden).Wait();
            dynamic status = GetStatus(game1, "no", OK).Result;
            Assert.AreEqual("active", (string)status.GameState);
            Assert.AreEqual(16, ((string)status.Board).Length);
            Assert.AreEqual(15, (int)status.TimeLimit);
            Assert.IsTrue((int)status.TimeLeft <= 120 && (int)status.TimeLeft > 0);
            Assert.AreEqual("Player 1", (string)status.Player1.Nickname);
            Assert.AreEqual(0, (int)status.Player1.Score);
            Assert.AreEqual("Player 2", (string)status.Player2.Nickname);
            Assert.AreEqual(0, (int)status.Player2.Score);
        }

        /// <summary>
        /// Try to playing a word.
        /// </summary>
        [TestMethod]
        public void TestPlayWord1()
        {
            String player1 = MakeUser("Player 1", Created).Result;
            String player2 = MakeUser("Player 2", Created).Result;
            String player3 = MakeUser("Player 3", Created).Result;
            String game1 = JoinGame(player1, 10, Accepted).Result;
            PlayWord(player1, game1, "a", Conflict).Wait();
            String game2 = JoinGame(player2, 30, Created).Result;
            Assert.AreEqual(game1, game2);

            PlayWord(player1, game1, null, Forbidden).Wait();
            PlayWord(player1, game1, "  ", Forbidden).Wait();
            PlayWord(null, game1, "a", Forbidden).Wait();
            PlayWord("blank", game1, "a", Forbidden).Wait();
            PlayWord(player3, game1, "a", Forbidden).Wait();
            PlayWord(player1, "blank", "a", Forbidden).Wait();

            Assert.AreEqual(-1, PlayWord(player1, game1, "xxxxxx", OK).Result);
            Assert.AreEqual(-1, PlayWord(player2, game1, "xxxxxx", OK).Result);

            Assert.AreEqual(0, PlayWord(player1, game1, "xxxxxx", OK).Result);
            Assert.AreEqual(0, PlayWord(player2, game1, "xxxxxx", OK).Result);

            Assert.AreEqual(0, PlayWord(player1, game1, "q", OK).Result);
            Assert.AreEqual(0, PlayWord(player2, game1, "q", OK).Result);
        }

        /// <summary>
        /// Try to play a lot of words.
        /// </summary>
        [TestMethod]
        public void TestPlayWord2()
        {
            // Time limit of game in seconds
            int LIMIT = 30;

            String player1 = MakeUser("Player 1", Created).Result;
            String player2 = MakeUser("Player 2", Created).Result;
            String player3 = MakeUser("Player 3", Created).Result;
            String game1 = JoinGame(player1, LIMIT, Accepted).Result;
            String game2 = JoinGame(player2, LIMIT, Created).Result;
            Assert.AreEqual(game1, game2);

            string board = GetStatus(game1, "no", OK).Result.Board;

            // Play up to LIMIT words
            int limit = LIMIT;
            foreach (string word in AllValidWords(board))
            {
                if (limit-- == 0) break;
                if (word.Length >= 3)
                {
                    Console.WriteLine(word);
                    Assert.AreEqual(GetScore(word), PlayWord(player1, game1, word, OK).Result);
                    Assert.AreEqual(GetScore(word), PlayWord(player2, game1, word, OK).Result);
                }
            }
        }

        /// <summary>
        /// Gets the status and asserts that it is as described in the parameters.
        /// </summary>
        private void CheckStatus(string game, string state, string brief, string p1, string p2, string n1, string n2, string b,
                                 List<string> w1, List<string> w2, List<int> s1, List<int> s2, int timeLimit)
        {
            dynamic status = GetStatus(game, brief, OK).Result;
            Assert.AreEqual(state, (string)status.GameState);

            if (state == "pending")
            {
                Assert.IsNull(status.TimeLimit);
                Assert.IsNull(status.TimeLeft);
                Assert.IsNull(status.Board);
                Assert.IsNull(status.Player1);
                Assert.IsNull(status.Player2);
            }
            else if (brief == "yes")
            {
                Assert.IsNull(status.TimeLimit);
                Assert.IsNull(status.Board);
                Assert.IsNull(status.Player1.WordsPlayed);
                Assert.IsNull(status.Player1.Nickname);
                Assert.IsNull(status.Player2.WordsPlayed);
                Assert.IsNull(status.Player2.Nickname);
            }
            else if (state == "active")
            {
                Assert.IsNull(status.Player1.WordsPlayed);
                Assert.IsNull(status.Player2.WordsPlayed);
            }

            if (state == "active" || state == "completed")
            {
                Assert.IsTrue((int)status.TimeLeft <= timeLimit);
                if (state == "active")
                {
                    Assert.IsTrue((int)status.TimeLeft > 0);
                }
                else
                {
                    Assert.IsTrue((int)status.TimeLeft >= 0);
                }

                int total1 = 0;
                for (int i = 0; i < s1.Count; i++)
                {
                    total1 += s1[i];
                }
                Assert.AreEqual(total1, (int)status.Player1.Score);

                int total2 = 0;
                for (int i = 0; i < s2.Count; i++)
                {
                    total2 += s2[i];
                }
                Assert.AreEqual(total2, (int)status.Player2.Score);

                if (brief != "yes")
                {
                    Assert.AreEqual(b, (string)status.Board);
                    Assert.AreEqual(timeLimit, (int)status.TimeLimit);
                    Assert.AreEqual(n1, (string)status.Player1.Nickname);
                    Assert.AreEqual(n2, (string)status.Player2.Nickname);

                    if (state == "completed")
                    {
                        List<dynamic> words1 = new List<dynamic>(status.Player1.WordsPlayed);
                        List<dynamic> words2 = new List<dynamic>(status.Player2.WordsPlayed);
                        Assert.AreEqual(w1.Count, words1.Count);
                        Assert.AreEqual(w2.Count, words2.Count);

                        for (int i = 0; i < w1.Count; i++)
                        {
                            Assert.AreEqual(w1[i], (string)words1[i].Word);
                            Assert.AreEqual(s1[i], (int)words1[i].Score);
                        }

                        for (int i = 0; i < w2.Count; i++)
                        {
                            Assert.AreEqual(w2[i], (string)words2[i].Word);
                            Assert.AreEqual(s2[i], (int)words2[i].Score);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Try to play a lot of words while checking status.
        /// </summary>
        [TestMethod]
        public void TestPlayWord3()
        {
            // Play for LIMIT seconds
            int LIMIT = 30;
            var words1 = new List<string>();
            var words2 = new List<string>();
            var scores1 = new List<int>();
            var scores2 = new List<int>();

            String player1 = MakeUser("Player 1", Created).Result;
            String player2 = MakeUser("Player 2", Created).Result;
            String game1 = JoinGame(player1, LIMIT, Accepted).Result;
            CheckStatus(game1, "pending", "no", player1, "", "", "", "", words1, words2, scores1, scores2, 0);
            String game2 = JoinGame(player2, LIMIT, Created).Result;
            Assert.AreEqual(game1, game2);

            DateTime startTime = DateTime.Now;

            string board = GetStatus(game1, "no", OK).Result.Board;

            CheckStatus(game1, "active", "no", player1, player2, "Player 1", "Player 2", board, words1, words2, scores1, scores2, 30);

            int limit = LIMIT;
            PlayWord(player1, game1, "xyzzy", OK).Wait();
            words1.Add("xyzzy");
            scores1.Add(-1);
            foreach (string word in AllValidWords(board))
            {
                if (limit-- == 0) break;
                Assert.AreEqual(GetScore(word), PlayWord(player1, game1, word, OK).Result);
                words1.Add(word);
                scores1.Add(GetScore(word));
                Assert.AreEqual(GetScore(word), PlayWord(player2, game1, word, OK).Result);
                words2.Add(word);
                scores2.Add(GetScore(word));
                CheckStatus(game1, "active", "no", player1, player2, "Player 1", "Player 2", board, words1, words2, scores1, scores2, 30);
                CheckStatus(game1, "active", "yes", player1, player2, "Player 1", "Player 2", board, words1, words2, scores1, scores2, 30);
            }

            // Wait until the game is over before checking the final status.
            int timeRemaining = LIMIT - (int) Math.Ceiling(DateTime.Now.Subtract(startTime).TotalSeconds);
            Thread.Sleep((timeRemaining + 2)*1000);

            CheckStatus(game1, "completed", "no", player1, player2, "Player 1", "Player 2", board, words1, words2, scores1, scores2, 30);
            CheckStatus(game1, "completed", "yes", player1, player2, "Player 1", "Player 2", board, words1, words2, scores1, scores2, 30);
            PlayWord(player1, game1, "a", Conflict).Wait();
            PlayWord(player1, game1, "b", Conflict).Wait();
        }

        /// <summary>
        /// Test game timing
        /// </summary>
        [TestMethod]
        public void TestTiming ()
        {
            String player1 = MakeUser("Player 1", Created).Result;
            String player2 = MakeUser("Player 2", Created).Result;
            String game1 = JoinGame(player1, 10, Accepted).Result;
            String game2 = JoinGame(player2, 10, Created).Result;
            string board = GetStatus(game1, "no", OK).Result.Board;

            Task t = new Task(() => TimerTester(game1, 10));
            t.Start();
            t.Wait();

            CheckStatus(game1, "completed", "yes", player1, player2, "Player 1", "Player 2", null, null, null, new List<int>(), new List<int>(), 10);
            CheckStatus(game1, "completed", "no", player1, player2, "Player 1", "Player 2", board, new List<string>(), new List<string>(), new List<int>(), new List<int>(), 10);

        }

        /// <summary>
        /// Helper for checking that times are reported semi-accurately.
        /// </summary>
        private void TimerTester (string game1, int limit)
        {
            while (limit >= 0)
            {
                int timeRemaining = GetStatus(game1, "yes", OK).Result.TimeLeft;
                Assert.IsTrue(timeRemaining <= limit + 1 && timeRemaining >= limit - 1);
                limit--;
                Thread.Sleep(1000);
            }
            Thread.Sleep(1000);
        }
    }
}
