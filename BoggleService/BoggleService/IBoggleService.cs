using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace Boggle
{
    [ServiceContract]
    public interface IBoggleService
    {
        [WebInvoke(Method = "POST", UriTemplate = "/users")]
        UserTokenObject CreateUser(NicknameObject name);

        [WebInvoke(Method = "POST", UriTemplate = "/games")]
        GameIDObject JoinGame(JoinGameInput input);

        [WebInvoke(Method = "PUT", UriTemplate = "/games")]
        void CancelJoin(UserTokenObject token);

        [WebInvoke(Method = "PUT", UriTemplate = "/games/{GameID}")]
        ScoreObject PlayWord(PlayWordInput input, string GameID);

        [WebGet(UriTemplate = "/games/{GameID}?brief={bs}")]
        GameStatus GetStatus(string GameID, string bs);

    }
}
