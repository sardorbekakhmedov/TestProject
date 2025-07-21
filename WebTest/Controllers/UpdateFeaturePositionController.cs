using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace WebTest.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class UpdateFeaturePositionController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UpdateFeaturePositionController> _logger;
    private const string ConnectionStringPROD = "Host=192.168.3.101;Port=5252;Database=dbcorporateex;Username=dev;Password=P@$$w0rd";
    private const string ConnectionStringTEST = "Host=185.100.53.216;Port=5656;Database=dbcorporateex_test;Username=cprn_test;Password=NYMrtY4ycYCL3sq8;MaxPoolSize=1024";


    public UpdateFeaturePositionController(
        IWebHostEnvironment environment,
        ILogger<UpdateFeaturePositionController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> UpdateFeaturePosition()
    {
        var outputLines1 = new List<string>();
        var outputLines2 = new List<string>();
        var outputLines3 = new List<string>();
        
        //  Персональный компьютер
        var standardFeatureId1 = 131;  // PROD = 131
        var file1Path1 = Path.Combine(_environment.ContentRootPath, "NewFile1.txt");
        outputLines1.Add("===============>  Персональный компьютер  <===================");
        await UpdateFeaturePositionAsync(file1Path1, standardFeatureId1, outputLines1);
        outputLines1.Add("==============================================================");
        
        // Моноблок
        var standardFeatureId2 = 140;  // PROD = 140
        var file1Path2 = Path.Combine(_environment.ContentRootPath, "NewFile2.txt");
        outputLines2.Add("=================>  Моноблок <===================");
        await UpdateFeaturePositionAsync(file1Path2, standardFeatureId2, outputLines2);
        outputLines2.Add("==============================================================");
        
        // Интерактивная сенсорная панель 
        var standardFeatureId3 = 130;  // PROD = 130
        var file1Path3 = Path.Combine(_environment.ContentRootPath, "NewFile3.txt");
        outputLines3.Add("=================>  Интерактивная сенсорная панель  <===================");
        await UpdateFeaturePositionAsync(file1Path3, standardFeatureId3, outputLines3);
        outputLines3.Add("==============================================================");
        
        var allList = outputLines1.Union(outputLines2).Union(outputLines3);
        var outputPath = Path.Combine(_environment.ContentRootPath, "output.txt");
        await System.IO.File.WriteAllLinesAsync(outputPath,  allList);
        
        return Ok(allList);
    }

    private async Task UpdateFeaturePositionAsync(string filePath, int standardFeatureId, List<string> outputLines)
    {
        var positionNumber = 1;

        var file1Lines = (await System.IO.File.ReadAllLinesAsync(filePath))
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        
        foreach (var line in file1Lines)
        {
            var ids = await UpdateFeaturePositionNumberAsync(line, standardFeatureId, positionNumber);
            outputLines.Add($"FeatureID:  {string.Join(',', ids)},  FeatureName:  {line},   Position:  {positionNumber}");
            positionNumber++;
        }

        _logger.LogInformation("Updated feature positions:\n{Lines}", string.Join(Environment.NewLine, outputLines));
    }

    
    private async Task<List<int>> UpdateFeaturePositionNumberAsync(string featureName, int standardFeatureId, int newPositionNumber)
    {
        var selectSql = @"
            SELECT f.id
            FROM standard_offer.features f
            WHERE name ->> 'uz' ILIKE @FeatureName
              AND standard_feature_id = @StandardFeatureId;";

        var updateSql = @"
            UPDATE standard_offer.features
            SET position_number = @NewPositionNumber
            WHERE id = @Id;";

        var featureIds = new List<int>();
        
        using (var connection = new NpgsqlConnection(ConnectionStringTEST))
        using (var selectCommand = new NpgsqlCommand(selectSql, connection))
        {
            selectCommand.Parameters.AddWithValue("@FeatureName", $"%{featureName}%");
            selectCommand.Parameters.AddWithValue("@StandardFeatureId", standardFeatureId);

            await connection.OpenAsync();


            // Step 1: Select matching feature IDs
            using (var reader = await selectCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    featureIds.Add(reader.GetInt32(0));
                }
            }

            // Step 2: Update each matched feature
            foreach (var id in featureIds)
            {
                using (var updateCommand = new NpgsqlCommand(updateSql, connection))
                {
                    updateCommand.Parameters.AddWithValue("@NewPositionNumber", newPositionNumber);
                    updateCommand.Parameters.AddWithValue("@Id", id);
                    await updateCommand.ExecuteNonQueryAsync();
                }
            }
        }

        return featureIds;
    }
    

}

public class FeatureResult
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int PositionNumber { get; set; }
}
