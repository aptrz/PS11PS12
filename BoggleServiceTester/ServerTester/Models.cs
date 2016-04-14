using System.Collections.Generic;

namespace Boggle
{
    public class Name
    {
        public string Nickname { get; set; }
    }

    public class User
    {
        public string UserToken { get; set; }
    }

    public class Game
    {
        public string GameToken { get; set; }
    }

    public class PlayedWord
    {
        public string UserToken { get; set; }

        public string Word { get; set; }
    }

    public class WordScore
    {
        public int Score { get; set; }
    }

    public class Status
    {
        public string GameState { get; set; }
        public string Board { get; set; }
        public int TimeLimit { get; set; }
        public int TimeLeft { get; set; }
        public Player Player1 { get; set; }
        public Player Player2 { get; set; }
    }

    public class Player
    {
        public string Nickname { get; set; }
        public int Score { get; set; }
        public List<WordAndScore> WordsPlayed { get; set; }
    }

    public class WordAndScore
    {
        public string Word { get; set; }

        public int Score { get; set; }
    }
}