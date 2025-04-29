using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace WebTest.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class SendToMinfinRabbitController : ControllerBase
{
    private int _count = 0;
    private int _errorCount = 0;
    
    [HttpPost]
    public async Task<IActionResult> SendToMinfinRabbit(ICollection<string> contractNumbers)
    {
        foreach (var contractNumber in contractNumbers)
        {
            var lotId = await GetLotIdByContractNumberAsync(contractNumber);
            
            try
            {
                if (lotId != null)
                {
                    await SendResultAsync((decimal)lotId, "RESULTAT");
                    _count++;

                }
            }
            catch (Exception e)
            {
                _errorCount++;
                Console.WriteLine(e);
                throw;
            }
        } 
        
        Console.WriteLine("\n\n =================>>>  FINISH success COUNT: " + _count + "  <<< ===================");
        Console.WriteLine(" =================>>>  FINISH Error soni:  " + _errorCount + "  <<< ===================");
        
        return Ok(new { successCount = _count,  errorCount = _errorCount });
    }
    
    private async Task SendResultAsync(decimal budgetLotId, string method)
    {
        using var httpClient = new HttpClient();
        var username = "BudgetCooperNew";
        var password = "BuDget877345@7$_!Adyu0";
        var authToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

        var url = $"http://192.168.122.22:1216/api/Test/SendToMinfinRabbit?budgetLotId={budgetLotId}&method={method}";

        try
        {
            var response = await httpClient.GetAsync(url);

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
    
    private string GetQueryResultatFromContract()
    {
        return @"
        SELECT
            bl.lot_id
        FROM shop.lots lot
        join shop.contract_docs cd on lot.id = cd.lot_id
        join budget.budget_lots bl on lot.id = bl.new_lot_id
        WHERE cd.contract_number = 'N1031425' and cd.status_id = 102 and bl.rmq_status = 'REQUEST_ETP';";
    }
    

    private async Task<decimal?> GetLotIdByContractNumberAsync(string contractNumber)
    {
        var connectionString = "Host=192.168.3.101;Port=5252;Database=dbcorporateex;Username=dev;Password=P@$$w0rd";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        
        try
        {
            var query = @$"
                 SELECT
                    bl.lot_id
                FROM shop.lots lot
                join shop.contract_docs cd on lot.id = cd.lot_id
                join budget.budget_lots bl on lot.id = bl.new_lot_id
                WHERE cd.contract_number = @contractNumber and bl.rmq_status = 'REQUEST_ETP' and deal.status_id = 102;";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@contractNumber", contractNumber);

            var result = await command.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return (long)result;
            }

            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}