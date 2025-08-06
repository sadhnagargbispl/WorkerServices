using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WorkerService1
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        private readonly string constr1;
        private readonly string constr2;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Read connection strings from appsettings.json
            constr1 = _configuration.GetConnectionString("SecondaryConnection");
            constr2 = _configuration.GetConnectionString("DefaultConnection");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SendMessages();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in ExecuteAsync: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(1000), stoppingToken);
            }
        }

        private async Task SendMessages()
        {
            using (SqlConnection conn = new SqlConnection(constr1))
            {
                await conn.OpenAsync();

                // LIMIT to avoid flooding (customize TOP value as needed)
                string sql = "SELECT  * FROM V#MessageSent";
                SqlDataAdapter da = new SqlDataAdapter(sql, conn);
                DataTable dt = new DataTable();
                da.Fill(dt);

                foreach (DataRow row in dt.Rows)
                {
                    string formNo = row["FormNo"].ToString();
                    string mobl = row["MobileNo"].ToString();
                    string msg = row["MessageText"].ToString();

                    string sResult = DateTime.Now.ToString("yyyyMMddHHmmssfff") + new Random().Next(0, 999).ToString("D3");

                    string url = "https://wa.bisplindia.in/api/method/frappe_whatsapp.whatsapp_chat.send_outgoing_text_message";
                    string jsonData = $"{{\"mobile_no\": \"{mobl}\", \"message\": \"{msg}\"}}";

                    await LogRequest(sResult, url, jsonData);

                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", "token b246f118c913831:64827093075409a");

                        StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                        try
                        {
                            var response = await client.PostAsync(url, content);
                            string responseText = await response.Content.ReadAsStringAsync();
                            await LogResponse(sResult, responseText);
                            await UpdateMessageSent(formNo, mobl);
                            _logger.LogInformation($"✅ Message sent to {mobl} (FormNo: {formNo})");
                        }
                        catch (Exception ex)
                        {
                            await LogResponse(sResult, ex.Message);
                            _logger.LogError($"❌ Failed to send message to {mobl}: {ex.Message}");
                        }
                    }

                    await Task.Delay(200); // slight delay between messages (rate limiting)
                }
            }
        }

        private async Task LogRequest(string reqId, string url, string postData)
        {
            string sql = "INSERT INTO Tbl_ApiRequest_ResponseWhatsapp (ReqID, Request, postdata, CompID) VALUES (@ReqID, @Request, @PostData, '1007')";
            using (SqlConnection conn = new SqlConnection(constr2))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ReqID", reqId);
                cmd.Parameters.AddWithValue("@Request", url);
                cmd.Parameters.AddWithValue("@PostData", postData);
                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task LogResponse(string reqId, string response)
        {
            string sql = "UPDATE Tbl_ApiRequest_ResponseWhatsapp SET Response = @Response WHERE ReqID = @ReqID";
            using (SqlConnection conn = new SqlConnection(constr2))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Response", response);
                cmd.Parameters.AddWithValue("@ReqID", reqId);
                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task UpdateMessageSent(string formNo, string mobileNo)
        {
            string sql = "UPDATE MessageSendLog SET IsSent = '1', SentDate = GETDATE() WHERE MobileNo = @MobileNo AND FormNo = @FormNo";
            using (SqlConnection conn = new SqlConnection(constr2))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@MobileNo", mobileNo);
                cmd.Parameters.AddWithValue("@FormNo", formNo);
                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}

//namespace WorkerService1
//{
//    public class Worker : BackgroundService
//    {
//        private readonly ILogger<Worker> _logger;

//        public Worker(ILogger<Worker> logger)
//        {
//            _logger = logger;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                if (_logger.IsEnabled(LogLevel.Information))
//                {
//                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
//                }
//                await Task.Delay(1000, stoppingToken);
//            }
//        }
//    }
//}
