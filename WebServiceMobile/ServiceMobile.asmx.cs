using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Services;

namespace WebServiceMobile
{
    [WebService(Namespace = "http://androiddev.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [System.Web.Script.Services.ScriptService]
    public class ServiceMobile : WebService
    {

        #region Properties
        private string idGeo = "dd540f1f-44ac-4929-a4e8-777a5d9b66b3";
        private string key = "n9tNJicGUqLc6KbDzeGoVNoDcNC70rjEgrXrKM8a";
        string BASE_URL = "http://ws_geosolution.geosolution.com.ar/mobile_test/Mobile/";
        private const string cnnString = "Server=08b75c75-cfac-4d9b-b023-a39b01057665.sqlserver.sequelizer.com;Database=db08b75c75cfac4d9bb023a39b01057665;User ID=dkeybpcggpoutvaf;Password=CJPQEYNWiXiAY5TUxzy8DHJ3sbQDHbPEGZkyK3ZrTvYnAMytZWzuzbR4aVwCiing;";
        #endregion

        #region Web Methods

        [WebMethod]
        public string GetHourMobile(string stop, string bus)
        {
            bool isNoData = true;
            string res = "";
            while (isNoData)
            {
                res = GetData(stop, bus);
                if (res.Contains("ErrorMessage") && res.Contains("nivel"))
                { }
                else
                    isNoData = false;
            }
            return res;
        }

        [WebMethod]
        public string GetNearStopsByBus(string bus, string lat, string lng)
        {
            bool isNoData = true;
            string res = "";
            while (isNoData)
            {
                res = StopsByBus(bus, lat, lng);
                if (res.Contains("ErrorMessage") && res.Contains("nivel"))
                { }
                else
                    isNoData = false;
            }
            return res;
        }

        [WebMethod]
        public string GetPositionByStop(string stop)
        {
            bool isNoData = true;
            string res = "";
            while (isNoData)
            {
                res = GetParadaStop(stop);
                if (res.Contains("ErrorMessage") && res.Contains("nivel"))
                { }
                else
                    isNoData = false;
            }
            return res;
        }

        [WebMethod]
        public void GetFeeds()
        {
            int cant = Helper.ReadFeeds();
            if (cant > 0)
            {
                Helper.GCMNotification(cant);
            }
        }

        [WebMethod]
        public string AddNewGCMToken(string token)
        {
            try
            {
                if (token.Length < 129) return "Error. Invalid Token";
                string tkn1 = token.Substring(0, 128);
                string tkn2 = token.Replace(tkn1, "");

                SqlConnection cnn = new SqlConnection(cnnString);
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM GCM_News WHERE GCMToken='" + tkn1 + "' AND GCMToken2='" + tkn2 + "' " +
                                                "SELECT MAX(ID) FROM GCM_News", cnn);
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                cnn.Open();
                DataSet ds = new DataSet();
                adapter.Fill(ds);
                int maxId = 1;

                if (ds.Tables.Count > 0)
                {
                    if (ds.Tables[1].Rows.Count > 0)
                    {
                        if (!string.IsNullOrEmpty(ds.Tables[1].Rows[0][0].ToString()))
                        {
                            maxId = Int32.Parse(ds.Tables[1].Rows[0][0].ToString());
                            maxId++;
                        }
                    }

                    if (ds.Tables[0].Rows.Count > 0)
                    {
                        if (!string.IsNullOrEmpty(ds.Tables[0].Rows[0][0].ToString()))
                        {
                            int exite = Int32.Parse(ds.Tables[0].Rows[0][0].ToString());
                            if (exite != 0) return "Already Exists";
                        }
                    }
                }
                else return "Error";


                string query = "INSERT INTO GCM_News VALUES(" + maxId + ",'" + tkn1 + "', '" + Guid.NewGuid().ToString() + "', '" + DateTime.Now.ToString("s") + "', '" + tkn2 + "')";
                SqlCommand cmd2 = new SqlCommand(query, cnn);
                cmd2.ExecuteNonQuery();
                cnn.Close();

                return "OK" + maxId;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }

        [WebMethod]
        public string DeleteToken(string token)
        {
            try
            {
                SqlConnection cnn = new SqlConnection(cnnString);
                SqlCommand cmd =
                    new SqlCommand("DELETE FROM GCM_News WHERE ID='" + token + "'",
                        cnn);
                cnn.Open();
                cmd.ExecuteNonQuery();
                cnn.Close();
                return "OK";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }

        #endregion

        #region Private Methods

        private string GetData(string parada, string linea)
        {
            DateTime date = DateTime.Now;
            string dayToSend = date.ToString("yyyy-MM-ddThh:mm:ss");
            string mensajeFecha = date.ToString("yyyyMMddhhmmss");
            string hashBuild = "";
            string mensaje = linea + parada + mensajeFecha;

            byte[] keyArray;
            using (HMACMD5 m = new HMACMD5(UTF8Encoding.UTF8.GetBytes(key)))
            {
                keyArray = m.ComputeHash(UTF8Encoding.UTF8.GetBytes(mensaje));
            }

            hashBuild = Convert.ToBase64String(keyArray, 0, keyArray.Length);

            string uri = BASE_URL + "CalcularMobile?" +
                         "parada=" + parada + "&linea=" + linea +
                         "&id=" + idGeo +
                         "&hash=" + hashBuild + "&fecha=" + dayToSend;

            WebRequest req = WebRequest.Create(uri);
            WebResponse resp = req.GetResponse();

            StreamReader sr = new StreamReader(resp.GetResponseStream());
            return sr.ReadToEnd().Trim();
        }

        private string StopsByBus(string linea, string lat, string lng)
        {
            DateTime date = DateTime.Now;
            string dayToSend = date.ToString("yyyy-MM-ddThh:mm:ss");
            string mensajeFecha = date.ToString("yyyyMMddhhmmss");
            string hashBuild = "";
            byte[] keyArray;
            using (HMACMD5 m = new HMACMD5(UTF8Encoding.UTF8.GetBytes(key)))
            {
                keyArray = m.ComputeHash(UTF8Encoding.UTF8.GetBytes(mensajeFecha));
            }

            hashBuild = Convert.ToBase64String(keyArray, 0, keyArray.Length);

            string uri = BASE_URL + "ObtenerParadasCercanasPorLinea?" +
                         "linea=" + linea +
                         "&longitud=" + lng + "&latitud=" + lat +
                         "&id=" + idGeo +
                         "&hash=" + hashBuild + "&fecha=" + dayToSend;

            WebRequest req = WebRequest.Create(uri);
            WebResponse resp = req.GetResponse();

            StreamReader sr = new StreamReader(resp.GetResponseStream());
            return sr.ReadToEnd().Trim();
        }

        private string GetParadaStop(string parada)
        {
            DateTime date = DateTime.Now;
            string dayToSend = date.ToString("yyyy-MM-ddThh:mm:ss");
            string mensajeFecha = date.ToString("yyyyMMddhhmmss");
            string hashBuild = "";
            byte[] keyArray;
            using (HMACMD5 m = new HMACMD5(UTF8Encoding.UTF8.GetBytes(key)))
            {
                keyArray = m.ComputeHash(UTF8Encoding.UTF8.GetBytes(mensajeFecha));
            }

            hashBuild = Convert.ToBase64String(keyArray, 0, keyArray.Length);

            string uri = BASE_URL + "ObtenerParada?" +
                         "parada=" + parada +
                         "&id=" + idGeo +
                         "&hash=" + hashBuild + "&fecha=" + dayToSend;

            WebRequest req = WebRequest.Create(uri);
            WebResponse resp = req.GetResponse();

            StreamReader sr = new StreamReader(resp.GetResponseStream());
            return sr.ReadToEnd().Trim();
        }

        #endregion
    }
}