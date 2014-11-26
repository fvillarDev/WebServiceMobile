

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace WebServiceMobile
{
    public class Helper
    {
        private const string cnnString = "Server=08b75c75-cfac-4d9b-b023-a39b01057665.sqlserver.sequelizer.com;Database=db08b75c75cfac4d9bb023a39b01057665;User ID=dkeybpcggpoutvaf;Password=CJPQEYNWiXiAY5TUxzy8DHJ3sbQDHbPEGZkyK3ZrTvYnAMytZWzuzbR4aVwCiing;";

        public static DataSet DsFeeds = new DataSet();
        public static Dictionary<string, string> UserIdToken = new Dictionary<string, string>();
        public static Dictionary<string, string> UserIdTokenToSend = new Dictionary<string, string>();

        private const string API_KEY = "AIzaSyAHMcqlO5o7GrGXpf6gD_-ndY1wxLQp_t4";

        public static string SendGCMNotification(string apiKey, string postData, string postDataContentType = "application/json")
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateServerCertificate);

            //
            //  MESSAGE CONTENT
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);

            //
            //  CREATE REQUEST
            HttpWebRequest Request = (HttpWebRequest)WebRequest.Create("https://android.googleapis.com/gcm/send");
            Request.Method = "POST";
            Request.KeepAlive = false;
            Request.ContentType = postDataContentType;
            Request.Headers.Add(string.Format("Authorization: key={0}", apiKey));
            Request.ContentLength = byteArray.Length;

            Stream dataStream = Request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();

            //
            //  SEND MESSAGE
            try
            {
                WebResponse Response = Request.GetResponse();
                HttpStatusCode ResponseCode = ((HttpWebResponse)Response).StatusCode;
                if (ResponseCode.Equals(HttpStatusCode.Unauthorized) || ResponseCode.Equals(HttpStatusCode.Forbidden))
                {
                    return "Unauthorized - invalid token";
                }
                if (!ResponseCode.Equals(HttpStatusCode.OK))
                {
                    return "An error ocurred processing the request";
                }

                StreamReader Reader = new StreamReader(Response.GetResponseStream());
                string responseLine = Reader.ReadToEnd();
                Reader.Close();

                return responseLine;
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        private static bool ValidateServerCertificate(
                                            object sender,
                                            X509Certificate certificate,
                                            X509Chain chain,
                                            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public static void GCMNotification(int cant)
        {
            try
            {
                string DEVICE_TOKEN = "";
                //SqlConnection cnn = new SqlConnection(cnnString);
                //SqlCommand cmd = new SqlCommand("SELECT * FROM GCM_News", cnn);
                //SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                //cnn.Open();
                //DataSet ds = new DataSet();
                //adapter.Fill(ds);
                //cnn.Close();

                const string message = "Nuevas Noticias Disponibles";
                const string tickerText = "Nuevas Noticias Transporte Córdoba";
                const string contentTitle = "Informe Transporte Cordoba";
                string subText = cant + " noticias encontradas";
                if (cant == 1) { subText = cant + " noticia encontrada"; }

                foreach (KeyValuePair<string, string> pair in UserIdTokenToSend)
                {
                    DEVICE_TOKEN = pair.Value;
                    string postData =
                        "{ \"registration_ids\": [ \"" + DEVICE_TOKEN + "\" ], " +
                        "\"data\": {\"tickerText\":\"" + tickerText + "\", " +
                        "\"contentTitle\":\"" + contentTitle + "\", " +
                        "\"subText\":\"" + subText + "\", " +
                        "\"message\": \"" + message + "\"}}";

                    SendGCMNotification(API_KEY, postData);
                }
            }
            catch (Exception ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Lee los feeds de cada url
        /// </summary>
        /// <returns>Cantidad de feeds validos</returns>
        public static int ReadFeeds()
        {
            GetPreviousFeeds();
            GetUserIds();

            int cant = 0;
            List<string> rssUrlsList = new List<string> { "http://www.lavoz.com.ar/rss.xml", "http://www.diaadia.com.ar/rss.xml",
                "http://www.lmcordoba.com.ar/rss/ultimas.xml", "http://www.cba24n.com.ar/publico/tda_xml.php" }; //, "http://ersaurbano.com/feed"};

            foreach (string url in rssUrlsList)
            {
                try
                {
                    string result = GetPageAsString(new Uri(url));
                    if (string.IsNullOrEmpty(result)) continue;
                    DataSet rssData = new DataSet();
                    rssData.ReadXml(new StringReader(result), XmlReadMode.Auto);
                    string rawGuid = "";
                    if (url.Contains("lavoz") || url.Contains("cba24n"))
                    {
                        for (int i = 0; i < rssData.Tables["item"].Rows.Count; i++)
                        {
                            if (!ValidTitle(rssData.Tables["item"].Rows[i]["title"].ToString())) continue;
                            rawGuid = rssData.Tables["guid"].Rows[i]["guid_Text"].ToString();
                            cant++;
                            TitleNotExists(rssData.Tables["item"].Rows[i]["title"].ToString(), rawGuid);
                        }

                    }
                    else
                    {
                        cant += rssData.Tables["item"].Rows.Cast<DataRow>().Where(row => ValidTitle(row["title"].ToString())).Count(row => TitleNotExists(row["title"].ToString(), row["guid"].ToString()));
                    }
                }
                catch (Exception ex)
                {
                }
            }

            return cant;
        }

        private static void GetPreviousFeeds()
        {
            SqlConnection cnn = new SqlConnection(cnnString);
            string query = "SELECT * FROM GCM_Feeds";
            string queryDelete = "DELETE FROM GCM_Feeds WHERE FeedDate < '" + DateTime.Now.AddDays(-7).ToString("s") + "'";
            SqlCommand cmd = new SqlCommand(query, cnn);
            SqlCommand cmdDelete = new SqlCommand(queryDelete, cnn);
            SqlDataAdapter adapter = new SqlDataAdapter(cmd);
            cnn.Open();
            cmdDelete.ExecuteNonQuery();
            DsFeeds = new DataSet();
            adapter.Fill(DsFeeds);
            cnn.Close();
        }

        private static void GetUserIds()
        {
            UserIdToken = new Dictionary<string, string>();
            UserIdTokenToSend = new Dictionary<string, string>();
            SqlConnection cnn = new SqlConnection(cnnString);
            SqlCommand cmd = new SqlCommand("SELECT ID, GCMToken, GCMToken2 FROM GCM_News", cnn);
            SqlDataAdapter adapter = new SqlDataAdapter(cmd);
            cnn.Open();
            DataSet ds = new DataSet();
            adapter.Fill(ds);
            cnn.Close();

            if (ds.Tables.Count <= 0) return;
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                UserIdToken.Add(row[0].ToString(), row[1].ToString() + row[2].ToString());
            }
        }

        private static bool TitleNotExists(string title, string rawGuid)
        {
            string guid = GetGuidFromRaw(rawGuid);
            if (DsFeeds.Tables.Count <= 0)
            {
                UserIdTokenToSend = UserIdToken;
                InsertFeed(title, guid);
                return true;
            }

            if (DsFeeds.Tables[0].Rows.Count > 0)
            {
                bool equals = DsFeeds.Tables[0].Rows.Cast<DataRow>().Any(row => row[3].ToString().Equals(guid));

                if (equals)
                {
                    string[] ids = DsFeeds.Tables[0].Select("Guid='" + guid + "'")[0][2].ToString().Split(',');
                    foreach (string id in ids.Where(id => !UserIdToken.ContainsKey(id)))
                    {
                        string val = "";
                        UserIdTokenToSend.TryGetValue(id, out val);
                        if (!string.IsNullOrEmpty(val))
                            UserIdTokenToSend.Add(id, val);
                    }

                    UpdateFeedIds(guid);
                    return true;
                }
            }

            UserIdTokenToSend = UserIdToken;
            InsertFeed(title, guid);
            return true;
        }

        private static void InsertFeed(string title, string guid)
        {
            SqlConnection cnn = new SqlConnection(cnnString);
            string ids = String.Join(",", UserIdToken.Keys.ToArray());
            string query = "INSERT INTO GCM_Feeds VALUES('" + title + "', '" + DateTime.Now.ToString("s") + "', '" + ids + "', '" + guid + "')";
            SqlCommand cmd = new SqlCommand(query, cnn);
            cnn.Open();
            cmd.ExecuteNonQuery();
            cnn.Close();
        }

        private static void UpdateFeedIds(string guid)
        {
            SqlConnection cnn = new SqlConnection(cnnString);
            string ids = String.Join(",", UserIdToken.Keys.ToArray());
            ids += "," + String.Join(",", UserIdTokenToSend.Keys.ToArray());
            if (ids.EndsWith(","))
            {
                ids = ids.Substring(0, ids.Length - 1);
            }
            string query = "UPDATE GCM_Feeds SET UserId='" + ids + "' WHERE Guid='" + guid + "'";
            SqlCommand cmd = new SqlCommand(query, cnn);
            cnn.Open();
            cmd.ExecuteNonQuery();
            cnn.Close();
        }

        private static string GetPageAsString(Uri address)
        {
            string result = "";
            HttpWebRequest request = WebRequest.Create(address) as HttpWebRequest;
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                StreamReader reader = new StreamReader(response.GetResponseStream());
                result = reader.ReadToEnd();
            }
            return result;
        }

        private static bool ValidTitle(string title)
        {
            if (!title.Contains("paro") && !title.Contains("Paro")) return false;
            return title.Contains("transporte") || title.Contains("UTA") || title.Contains("colectivo") ||
                   title.Contains("colectivos") || title.Contains("choferes")
                   || title.Contains("chofer") || title.Contains("transportes");
        }

        private static string GetGuidFromRaw(string rawGuid)
        {
            if (rawGuid.Contains("lavoz") || rawGuid.Contains("diaadia"))
            {
                return rawGuid.Split(' ')[0];
            }
            if (rawGuid.Contains("lmcordoba"))
            {
                return rawGuid.Replace("http://www.lmcordoba.com.ar/nota.php?ni=", "");
            }
            return rawGuid.Contains("cba24n") ? rawGuid.Replace("http://www.cba24n.com.ar/node/", "") : "";
        }
    }
}