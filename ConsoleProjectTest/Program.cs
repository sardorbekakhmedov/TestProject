


decimal avans = 113;

double d = (long)(avans * 12) / (double)112;

Console.WriteLine(d);

d = (long)Math.Floor(d * 100); // или Math.Floor, если нужно просто отбросить дробную часть

Console.WriteLine(d); // 1071