using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Timers;

namespace BoggleClient
{

    public partial class BoggleView : Form
    {
        private static string letters, playerToken;
        private static string player1, player2 = "";
        private List<string> wordList;
        private BoggleView theView;
        private int gameToken;
        private dynamic currentState;


        public BoggleView()
        {
            //PostDemo();
            ShowDialog("Setup Connection", " Test stuff");
            InitializeComponent();
            theView = this;
            

            button1.Click += MakeWord;
            button2.Click += MakeWord;
            button3.Click += MakeWord;
            button4.Click += MakeWord;
            button5.Click += MakeWord;
            button6.Click += MakeWord;
            button7.Click += MakeWord;
            button8.Click += MakeWord;
            button9.Click += MakeWord;
            button10.Click += MakeWord;
            button11.Click += MakeWord;
            button12.Click += MakeWord;
            button13.Click += MakeWord;
            button14.Click += MakeWord;
            button15.Click += MakeWord;
            button16.Click += MakeWord;

            wordList = new List<string>();

        }

        public HttpClient CreateClient()
        {
            // Create a client whose base address is the GitHub server
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("http://bogglecs3500s16.azurewebsites.net");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public int ShowDialog(string text, string caption)
        {
            Form prompt = new Form();
            prompt.Width = 400;
            prompt.Height = 400;
            prompt.Text = caption;
            Label textLabel = new Label() { Left = 150, Top = 20, Text = text };
            Label labelName = new Label() { Left = 100, Top = 100, Text = "Name" };
            Label labelServer = new Label() { Left = 100, Top = 150, Text = "Server" };
            Label labelTime = new Label() { Left = 100, Top = 200, Text = "Duration" };
            prompt.Controls.Add(labelName);
            prompt.Controls.Add(labelServer);
            prompt.Controls.Add(labelTime);


            TextBox textName = new TextBox() { Left = 200, Top = 100, Text = "test" };
            TextBox textServer = new TextBox() { Left = 200, Top = 150, Text = "http://bogglecs3500s16.azurewebsites.net" };
            TextBox textTime = new TextBox() { Left = 200, Top = 200, Text = "100" };
            prompt.Controls.Add(textName);
            prompt.Controls.Add(textServer);
            prompt.Controls.Add(textTime);


            Button confirmButton = new Button() { Text = "OK", Left = 80, Width = 100, Top = 300 };
            Button cancelButton = new Button() { Text = "Cancel", Left = 200, Width = 100, Top = 300 };
            prompt.Controls.Add(confirmButton);
            prompt.Controls.Add(cancelButton);

            confirmButton.Click += (sender, e) => { CreateGame(textName.Text, textServer.Text, textTime.Text);prompt.Close(); };

            cancelButton.Click += (sender, e) => { CancelJoin(); }; 
            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textName);
            prompt.ShowDialog();
            return (int)1;
        }

        public async void CreateGame(string name, string division, string time)
        {
            //System.Timers.Timer myTimer = new System.Timers.Timer();
            //myTimer.Elapsed += new ElapsedEventHandler(DisplayTimeEvent);
            //myTimer.Interval = 1000;

            using (HttpClient client = CreateClient())
            {
                String userID;
                int t;
                dynamic data = new ExpandoObject();
                Int32.TryParse(time, out t);


                data.Nickname = name;

                StringContent content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
                HttpResponseMessage response = client.PostAsync("/BoggleService.svc/users", content).Result;

                //If user is created successfully
                if (response.IsSuccessStatusCode)
                {
                    userID = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine(userID);
                    var newRepo = JsonConvert.DeserializeObject<TestObject>(userID);
                    string s = newRepo.UserToken;

                    playerToken = data.UserToken = s;
                    data.TimeLimit = t;

                    content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
                    response = client.PostAsync("/BoggleService.svc/games", content).Result;

                    //If game is join successfully
                    if (response.IsSuccessStatusCode)
                    {
                        String result = response.Content.ReadAsStringAsync().Result;
                        var gameID = JsonConvert.DeserializeObject<TestObject>(result);
                        string gID = gameID.GameID;
                        Int32.TryParse(gID, out gameToken);

                        //Try and get game state to start game and populate board

                        await GameStatus();

                        while (currentState.GameState.Equals("pending"))
                        {

                            await GameStatus();
                        }

                        letters = currentState.Board;
                        player1 = name;


                        PopulateBoard();
                        ;

                    }
                    else
                    {
                        Console.WriteLine("Things Failed");
                    }
                }
                else
                {
                    Console.WriteLine("Error creating userID: " + response.StatusCode);
                    Console.WriteLine(response.ReasonPhrase);
                }
            }
        }

        public void CancelJoin()
        {
            using (HttpClient client = CreateClient())
            {
                HttpResponseMessage response = client.GetAsync("/BoggleService.svc/games/" + gameToken).Result;
                String hold = response.Content.ReadAsStringAsync().Result;
                currentState = JsonConvert.DeserializeObject<TestObject>(hold);
            }
        }

        public async Task GameStatus()
        {
            using (HttpClient client = CreateClient())
            {
                await Task.Delay(1000);
                HttpResponseMessage response = client.GetAsync("/BoggleService.svc/games/" + gameToken).Result;
                String hold = response.Content.ReadAsStringAsync().Result;
                currentState = JsonConvert.DeserializeObject<TestObject>(hold);
            }
        }

        public void PopulateBoard()
        {
            theView.NameLabel.Text = player1;

            // Big sloppy mess but takes care of the 'QU' problem
            for(int i = 0; i < 16; i++)
            {
                if(i == 0)
                {
                    if(letters[i].ToString().Equals("Q"))
                        theView.button1.Text = letters[i].ToString() + "U";
                    else
                        theView.button1.Text = letters[i].ToString();
                }
                else if (i == 1)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button2.Text = letters[i].ToString() + "U";
                    else
                        theView.button2.Text = letters[i].ToString();
                }
                else if (i == 2)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button3.Text = letters[i].ToString() + "U";
                    else
                        theView.button3.Text = letters[i].ToString();
                }
                else if (i == 3)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button4.Text = letters[i].ToString() + "U";
                    else
                        theView.button4.Text = letters[i].ToString();
                }
                else if (i == 4)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button5.Text = letters[i].ToString() + "U";
                    else
                        theView.button5.Text = letters[i].ToString();
                }
                else if (i == 5)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button6.Text = letters[i].ToString() + "U";
                    else
                        theView.button6.Text = letters[i].ToString();
                }
                else if (i == 6)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button7.Text = letters[i].ToString() + "U";
                    else
                        theView.button7.Text = letters[i].ToString();
                }
                else if (i == 7)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button8.Text = letters[i].ToString() + "U";
                    else
                        theView.button8.Text = letters[i].ToString();
                }
                else if (i == 8)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button9.Text = letters[i].ToString() + "U";
                    else
                        theView.button9.Text = letters[i].ToString();
                }
                else if (i == 9)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button10.Text = letters[i].ToString() + "U";
                    else
                        theView.button10.Text = letters[i].ToString();
                }
                else if (i == 10)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button11.Text = letters[i].ToString() + "U";
                    else
                        theView.button11.Text = letters[i].ToString();
                }
                else if (i == 11)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button12.Text = letters[i].ToString() + "U";
                    else
                        theView.button12.Text = letters[i].ToString();
                }
                else if (i == 12)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button13.Text = letters[i].ToString() + "U";
                    else
                        theView.button13.Text = letters[i].ToString();
                }
                else if (i == 13)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button14.Text = letters[i].ToString() + "U";
                    else
                        theView.button14.Text = letters[i].ToString();
                }
                else if (i == 14)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button15.Text = letters[i].ToString() + "U";
                    else
                        theView.button15.Text = letters[i].ToString();
                }
                else if (i == 15)
                {
                    if (letters[i].ToString().Equals("Q"))
                        theView.button16.Text = letters[i].ToString() + "U";
                    else
                        theView.button16.Text = letters[i].ToString();
                }
            }

        }

        public void MakeWord(object sender, EventArgs e)
        {
            string s = (sender as Button).Text;
            string constant = theView.NewWordText.Text;
            theView.NewWordText.Text = constant + s;
        }

        public void GameEnd()
        {

        }

        private void SubmitButton_Click(object sender, EventArgs e)
        {

            using (HttpClient client = CreateClient())
            {
                string display="";
                dynamic data = new ExpandoObject();
                data.UserToken = playerToken;
                data.Word = theView.NewWordText.Text.Trim();

                StringContent content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
                HttpResponseMessage response = client.PutAsync("/BoggleService.svc/games/"+ gameToken, content).Result;

                String hold = response.Content.ReadAsStringAsync().Result;
                if (response.IsSuccessStatusCode)
                {
                    dynamic wordScore = JsonConvert.DeserializeObject<TestObject>(hold);
                    int score;
                    int total;
                    int.TryParse(wordScore.Score, out score);
                    int.TryParse(theView.ScoreLabel.Text, out total);
                    theView.ScoreLabel.Text = (total + score).ToString();
                    WordListBox.Clear();

                    wordList.Add(theView.NewWordText.Text.Trim() + ": " + wordScore.Score);
                    foreach (var item in wordList)
                    {
                        display = display + item + "\r\n";
                    }
                    WordListBox.Text = display;

                    theView.NewWordText.Clear();
                }
                else
                {
                    GameEnd();
                }
            }

        }


        private void LeaveButton_Click(object sender, EventArgs e)
        {
            //theView.Hide();
            ShowDialog("Setup Connection", " Test stuff");
        }
    }

    public class TestObject
    {
        [JsonProperty("GameID")]
        public string GameID { get; set; }

        [JsonProperty("GameState")]
        public string GameState { get; set; }

        [JsonProperty("Score")]
        public string Score { get; set; }

        [JsonProperty("Word")]
        public string Word { get; set; }

        [JsonProperty("UserToken")]
        public string UserToken { get; set; }

        [JsonProperty("TimeLimit")]
        public string TimeLimit { get; set; }

        [JsonProperty("Board")]
        public string Board { get; set; }

        [JsonProperty("TimeLeft")]
        public string TimeLeft { get; set; }


    }
}
