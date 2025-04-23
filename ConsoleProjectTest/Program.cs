using System;
using System.Threading.Tasks;
using Npgsql;

class Program
{
    static async Task Main()
    {
        var statusDate = DateTime.Parse("2025-02-17 14:06:51.144025");
        
        Console.WriteLine(statusDate);
    }
}