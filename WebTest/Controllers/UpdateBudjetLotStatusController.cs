using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace WebTest.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class UpdateBudjetLotStatusController : ControllerBase
{
    private readonly ILogger<SendResultatController> _logger;
    private int _count = 0;
    private int _countNoResult = 0;
    private readonly List<string> _resultatAnswer = [];


    public UpdateBudjetLotStatusController(ILogger<SendResultatController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> RunAction()
    {
        await CreateQueryAsync();
        
        Console.WriteLine("\n\n =================>>>  FINISH success COUNT: " + _count + "  <<< ===================");
        
        return Ok(new { error = _resultatAnswer,  successCount = _resultatAnswer.Count });
    }

    private async Task CreateQueryAsync()
    {
        var connectionString = "Host=192.168.3.101;Port=5252;Database=dbcorporateex;Username=dev;Password=P@$$w0rd";

        var queryResultat = GetQueryMinFin();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        try
        {
            var items = new List<long>();

            await using (var command = new NpgsqlCommand(queryResultat, connection))
            {
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var lotId = reader.GetInt64(0);
                        items.Add(lotId);
                    }
                }
            }

            
            foreach (var budjetLotId in items)
            {
                try
                {
                    var text1 = $" BudjetLotId:   {budjetLotId},  Budjet lot Status:  ERROR_INFO,   Comment:   DONE!,   ";
                    
                    Console.WriteLine(text1);
                    Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");

                    await SendUpdateStatusBudjetLotAsync(budjetLotId, "ERROR_INFO");
                    _count++;
                    _resultatAnswer.Add(text1);
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
    }

    private async Task SendUpdateStatusBudjetLotAsync(decimal budgetLotId, string method)
    {
        const string username = "BudgetCooperNew";
        const string password = "BuDget877345@7$_!Adyu0";
        const string baseUrl = "http://192.168.122.22:1216/api/Test/UpdateStatusBudgetLot";
        
        try
        {
            using var httpClient = new HttpClient();
            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            
            var requestUrl = $"{baseUrl}?budgetLotId={budgetLotId}&method={method}";

            using var response = await httpClient.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Успешно отправлено: LotId = {budgetLotId}");
            }
            else
            {
                Console.WriteLine($"Ошибка при отправке: {(int)response.StatusCode} - {budgetLotId}");
                Console.WriteLine($"Ошибка при отправке: {(int)response.StatusCode} - {budgetLotId}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Исключение при отправке запроса: {e}");
            throw;
        }
    }

    private string GetQueryMinFin()
    {
        return @$"
            SELECT DISTINCT ON (mh.lot_id)
  
                    mh.lot_id
                            
            FROM budget.minfin_histories mh
            JOIN budget.budget_lots bl ON mh.lot_id = bl.lot_id
            JOIN shop.contract_docs cd ON bl.new_lot_id = cd.lot_id
            JOIN budget.error_info ei on bl.lot_id = ei.lot_id
            WHERE cd.contract_date >= '2025-04-07 16:32:07.269237'
              AND (cd.contract_number ILIKE 'N%' OR cd.contract_number ILIKE 'I%')
              AND mh.method = (
                                SELECT DISTINCT ON (mhh.lot_id)
                                mhh.method
                                FROM budget.minfin_histories mhh
                                where mhh.lot_id = mh.lot_id
                                ORDER BY mhh.lot_id, mhh.created_at DESC
                                )
              AND (mh.response::json->'PAYLOAD'->>'STATE')::int = 3
              AND bl.rmq_status = 'RESULTAT'
            ORDER BY mh.lot_id, mh.created_at DESC";
    }
  
}