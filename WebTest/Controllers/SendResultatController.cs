using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Npgsql;

namespace WebTest.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class SendResultatController : ControllerBase
{
    private readonly ILogger<SendResultatController> _logger;
    private int _count = 0;
    private int _countNoResult = 0;
    private readonly List<string> _resultatSuccess = [];
    private readonly List<string> _resultatAnswer = [];


    public SendResultatController(ILogger<SendResultatController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Test(ICollection<string> contractNumbers)
    {
        foreach (var contractNumber in contractNumbers)
        {
            await CreateQueryAsync(contractNumber);
        } 
        
        Console.WriteLine("\n\n =================>>>  FINISH success COUNT: " + _count + "  <<< ===================");
        Console.WriteLine(" =================>>>  FINISH Jo'natilmaganlar soni:  " + _countNoResult + "  <<< ===================");
        
        return Ok(new { error = _resultatAnswer, success = _resultatSuccess, successCount = _resultatSuccess.Count,  errorCount = _resultatAnswer.Count });
    }

    private async Task<(List<string>, List<string>)> CreateQueryAsync(string contractNumber)
    {
        var connectionString = "Host=192.168.3.101;Port=5252;Database=dbcorporateex;Username=dev;Password=P@$$w0rd";
       // var connectionStringPROD = "Host=192.168.122.23;Port=5432;Database=dbcorporateex;Username=cprn_prod;Password=P5fnBvw9xdBGquWKaLs7;MaxPoolSize=500;Pooling=true;;Timeout=30;Command Timeout=30";


        // var queryResultat = GetQueryResultat();
        var queryResultat = GetQueryResultatFromContract();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        try
        {
            var items = new List<RecordItem>();

            await using (var command = new NpgsqlCommand(queryResultat, connection))
            {
                command.Parameters.AddWithValue("contractNumber", contractNumber);
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var item = new RecordItem
                        {
                            ContractNumber = reader.GetString(0),
                            CustomerId = reader.GetInt32(1),
                            ProviderId = reader.GetInt32(2),
                            ContractSum = reader.GetDecimal(3),
                            NewLotId = reader.GetInt32(4),
                            BudgetLotId = reader.GetDecimal(5),
                            Json = reader.GetString(6),
                            DataTime = reader.GetDateTime(7),
                            BudjetLotPkId = reader.GetInt32(8),
                        };

                        items.Add(item);
                    }
                }
            }

            
            foreach (var item in items)
            {
                var (methodName, response) = await GetQueryLastMethodMinFinHistory(connection, item.BudgetLotId);
                
                if (   (methodName == "ERROR_INFO" && response != null && response.Contains("уже есть в системе")) 
                    || (methodName == "CONTRACT_INFO" && response != null && response.Contains("\"STATE\": 2")) 
                    || methodName == "SUCCESS_INFO" || methodName == "RESULTAT" || methodName == "QUERY_FACTURA" || methodName == "QUERY_PAYS_BY_LOTID")
                {

                    var text1 = $" ContractNumber:   {contractNumber},  Comment:   Oldin Resultat jo'natilgan!,  Last method:   {methodName}  ";
                    
                    _resultatAnswer.Add(text1);
                    
                    Console.WriteLine(text1);
                    Console.WriteLine("_________________________________________________________________________________________________________");
                    _countNoResult++;
                    continue;
                }
                    
                var docid = await GetDocIdByContractNumberAsync(connection, item.ContractNumber);

                if (docid == null)
                {
                    var text1 = $" ContractNumber:   {contractNumber},  Comment:  DocID null !,  Last method:   {methodName} ";
                    
                    _resultatAnswer.Add(text1);
                    Console.WriteLine(text1);
                    Console.WriteLine("_________________________________________________________________________________________________________");
                    _countNoResult++;
                    continue;
                    var guid = Guid.NewGuid();
                    docid = await InsertQueryContractDocumentAsync(connection, item.ContractNumber, item.CustomerId, item.ProviderId, item.NewLotId, guid);
                }

                try
                {
                    var result = JsonConvert.DeserializeObject<BudjetResult>(item.Json);

                    var firstPrePaidPercent = item.ContractSum >= 1_000_000_000 ? 15 : 30;
                    var taxPercent = 12;

                    var oldLinkLanguage = result!.PAYLOAD.LINKS.LastOrDefault()!.LINK;
                    oldLinkLanguage = oldLinkLanguage[^2..];

                    var newLink = $"https://new.cooperation.uz/ocelot/api-shop/Contract/DownloadContractFile?fileId={docid}&lang={oldLinkLanguage}";

                    var avansSum = (long)(item.ContractSum * firstPrePaidPercent) / 100;
                    double taxSum = (long)(item.ContractSum * taxPercent) / (double)112;
                    
                    avansSum *= 100;
                    taxSum = (long)Math.Floor(taxSum * 100);
                    
                    result.PAYLOAD.AVANS = avansSum;
                    result.PAYLOAD.SUMNDS = (long)taxSum;
                    result.PAYLOAD.PROC_ID = 19;
                    result.PAYLOAD.REESTR_ID = item.BudjetLotPkId;
                    result.PAYLOAD.GRAFICS.FirstOrDefault()!.AVANS = avansSum;
                    result.PAYLOAD.LINKS.LastOrDefault()!.LINK = newLink;
                    result.PAYLOAD.SPECIFICATIONS.FirstOrDefault().TOVAREDIZM = 1;

                    var text1 = $" ContractNumber:   {contractNumber},   Comment:   DONE!,   Last method:  RESULTAT ";
                    
                    Console.WriteLine(text1);
                    Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");

                    await SendResultAsync(result, item.BudgetLotId, "RESULTAT");
                    _count++;
                    _resultatSuccess.Add(text1);
                }
                catch (Exception jsonEx)
                {
                    Console.WriteLine($"Ошибка десериализации JSON: {jsonEx.Message}");
                }

                Console.WriteLine(" ===========>>> COUNT: " + _count);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка подключения или выполнения запроса: " + ex.Message);
        }

        return (_resultatAnswer, _resultatSuccess);
    }

    private async Task SendResultAsync(BudjetResult result, decimal budgetLotId, string method)
    {
        using var httpClient = new HttpClient();
        var username = "BudgetCooperNew";
        var password = "BuDget877345@7$_!Adyu0";
        var authToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

        var url = $"http://192.168.122.22:1216/api/Test/SendMessage?budgetLotId={budgetLotId}&method={method}";
        var jsonString = JsonConvert.SerializeObject(result);

        var postData = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("json", jsonString)
        };

        var content = new FormUrlEncodedContent(postData);

        try
        {
            var response = await httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Отправлено успешно: LotId={budgetLotId}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Ошибка: {(int)response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Исключение при отправке: {ex.Message}");
        }
    }

    private string GetQueryResultat()
    {
        return @$"
                SELECT DISTINCT ON (minfin_histories.lot_id)
                    cd.contract_number,
                    cd.customer_company_id,
                    cd.provider_company_id,
                    cd.contract_sum,
                    bl.new_lot_id,
                    minfin_histories.lot_id,
                    minfin_histories.request,
                    minfin_histories.created_at,
                    bl.id
                FROM budget.minfin_histories
                join budget.budget_lots bl on minfin_histories.lot_id = bl.lot_id
                join shop.contract_docs cd on bl.new_lot_id = cd.lot_id
                WHERE method = 'RESULTAT'
                  AND minfin_histories.lot_id IN (SELECT mh.lot_id
                                FROM budget.minfin_histories mh
                                join budget.budget_lots bl on mh.lot_id = bl.lot_id
                                WHERE method = 'CONTRACT_INFO' and mh.created_at >= '2025-04-07' and  ( bl.rmq_status = 'RESULTAT' or  bl.rmq_status = 'ERROR_INFO' ) and mh.response ilike '%аванс%')
                ORDER BY minfin_histories.lot_id, minfin_histories.created_at DESC;";
    }
    
    private string GetQueryResultatFromContract()
    {
        return @"
        SELECT DISTINCT ON (minfin_histories.lot_id)
            cd.contract_number,
            cd.customer_company_id,
            cd.provider_company_id,
            cd.contract_sum,
            bl.new_lot_id,
            minfin_histories.lot_id,
            minfin_histories.request,
            minfin_histories.created_at,
            bl.id
        FROM budget.minfin_histories
        JOIN budget.budget_lots bl ON minfin_histories.lot_id = bl.lot_id
        JOIN shop.contract_docs cd ON bl.new_lot_id = cd.lot_id
        WHERE method = 'RESULTAT' AND cd.contract_number = @contractNumber
        ORDER BY minfin_histories.lot_id, minfin_histories.created_at DESC;";
    }

    private async Task<(string? Method, string? Response)> GetQueryLastMethodMinFinHistory(NpgsqlConnection connection, decimal lotId)
    {
        try
        {
            var query = @$" 
            SELECT 
                mh.method,
                mh.response
            FROM budget.minfin_histories mh
            WHERE mh.lot_id = @lotId
            ORDER BY mh.created_at DESC
            LIMIT 1;";
    
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@lotId", lotId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var method = reader.IsDBNull(0) ? null : reader.GetString(0);
                var response = reader.IsDBNull(1) ? null : reader.GetString(1);

                return (method, response);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        return (null, null);
    }

    private async Task<Guid?> GetDocIdByContractNumberAsync(NpgsqlConnection connection, string contractNumber)
    {
        try
        {
            var query = @$"
                SELECT cd.doc_id
                FROM shop.contract_documents cd
                WHERE contract_number = @contractNumber;";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@contractNumber", contractNumber);

            var result = await command.ExecuteScalarAsync();
            if (result != null && Guid.TryParse(result.ToString(), out var docId))
            {
                return docId;
            }

            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task<Guid?> InsertQueryContractDocumentAsync(NpgsqlConnection connection, string contractNumber, int customerId, int providerId, int newLotId, Guid docId)
    {
        try
        {
            var query = @$"
                INSERT INTO shop.contract_documents (contract_number, custommer_id, provider_id, lot_id, is_qrcode_generated, doc_id)
                VALUES (@contractNumber, @customerId, @providerId, @newLotId, false, @docId)
                RETURNING doc_id";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@contractNumber", contractNumber);
            command.Parameters.AddWithValue("@customerId", customerId);
            command.Parameters.AddWithValue("@providerId", providerId);
            command.Parameters.AddWithValue("@newLotId", newLotId);
            command.Parameters.AddWithValue("@docId", docId);

            var result = await command.ExecuteScalarAsync();
            if (result != null && Guid.TryParse(result.ToString(), out var insertedDocId))
            {
                return insertedDocId;
            }

            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private class RecordItem
    {
        public string ContractNumber { get; set; }
        public int CustomerId { get; set; }
        public int ProviderId { get; set; }
        public decimal ContractSum { get; set; }
        public int NewLotId { get; set; }
        public decimal BudgetLotId { get; set; }
        public string Json { get; set; }
        public DateTime DataTime { get; set; }
        
        public int BudjetLotPkId { get; set; }

    }

    private class LotBaseDto
    {
        public long LOTID { get; set; }
    }

    private class BudjetResult
    {
        public int ETP_ID { get; set; }
        public int REQUEST_ID { get; set; }
        public string? REPLY_TO { get; set; }
        public string METHOD_NAME { get; set; }
        public QueryResultDto PAYLOAD { get; set; }
    }

    private class QueryResultDto : LotBaseDto
    {
        public int PROC_ID { get; set; }
        public string LOTDATE1 { get; set; }
        public string LOTDATE2 { get; set; }
        public string CONTRACTNUM { get; set; }
        public string CONTRACTDAT { get; set; }
        public int DVR { get; set; }
        public string ORGAN { get; set; }
        public string INN { get; set; }
        public string LS { get; set; }
        public string VENDORNAME { get; set; }
        public string VENDORBANK { get; set; }
        public string VENDORACC { get; set; }
        public string VENDORINN { get; set; }
        public string MALOY { get; set; }
        public long SUMMA { get; set; }
        public long SUMNDS { get; set; }
        public int SROK { get; set; }
        public long AVANS { get; set; }
        public int AVANSDAY { get; set; }
        public string CONTRACTBEG { get; set; }
        public string CONTRACTEND { get; set; }
        public string PURPOSE { get; set; }
        public string VENDORTERR { get; set; }
        public FINSRC[] FINSRC { get; set; }
        public SPECIFICATIONS[] SPECIFICATIONS { get; set; }
        public GRAFICS[] GRAFICS { get; set; }
        public LINKS[] LINKS { get; set; }
        public string BENEFICIAR { get; set; }
        public int RASCHOT { get; set; }
        public object REESTR_ID { get; set; }
        public string PNFL { get; set; }
        public string VENDORCOUNTRY { get; set; }
        public string VENDORFORIEGIN { get; set; }
        public string VENDORINFO { get; set; }
        public string VENDORKLS { get; set; }
        public object GEN_ID { get; set; }
        public object CONTRACT_ID { get; set; }
        public int VERSION { get; set; }
    }

    private class FINSRC
    {
        public int NPOS { get; set; }
        public string KLS { get; set; }
        public string BANKCODE { get; set; }
        public object BANKACC { get; set; }
        public long SUMMA { get; set; }
        public long AVANS { get; set; }
    }

    private class SPECIFICATIONS
    {
        public int NPOS { get; set; }
        public int FINYEAR { get; set; }
        public string KLS { get; set; }
        public string TOVAR { get; set; }
        public string TOVARNAME { get; set; }
        public string TOVARNOTE { get; set; }
        public int? TOVAREDIZM { get; set; }
        public decimal TOVARAMOUNT { get; set; }
        public long TOVARPRICE { get; set; }
        public long TOVARSUMMA { get; set; }
        public string EXPENSE { get; set; }
        public PROPERTY[] PROPERTIES { get; set; }
        public NOTE[] NOTE { get; set; }
        public SPLIT[] SPLIT { get; set; }
    }

    private class PROPERTY
    {
        public int PROP_NUMB { get; set; }
        public string PROP_NAME { get; set; }
        public int VAL_NUMB { get; set; }
        public string VAL_NAME { get; set; }
    }

    private class NOTE
    {
        public string MARKA { get; set; }
        public string TECHSPEC { get; set; }
        public string MANUFACTURER { get; set; }
        public string COUNTRY { get; set; }
        public string GARANT { get; set; }
        public string GODIZG { get; set; }
        public string SROKGOD { get; set; }
        public object LICENSE { get; set; }
    }

    private class SPLIT
    {
        public int MONTH { get; set; }
        public decimal TOVARAMOUNT { get; set; }
    }

    private class GRAFICS
    {
        public int FINYEAR { get; set; }
        public int MONTH { get; set; }
        public string KLS { get; set; }
        public long TOVARSUMMA { get; set; }
        public string EXPENSE { get; set; }
        public long AVANS { get; set; }
    }

    private class LINKS
    {
        public string FILENAME { get; set; }
        public string LINK { get; set; }
    }
}