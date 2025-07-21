namespace ConsoleApp1;

public abstract class Program
{
    static void Main()
    {
        var pc = new ProducerConsumer();

        // Producer thread
        Thread producerThread = new Thread(() =>
        {
            for (int i = 1; i <= 10; i++)
            {
                pc.Produce(i);
                Thread.Sleep(300); // Biroz kutish
            }
        });

        // Consumer thread
        Thread consumerThread = new Thread(() =>
        {
            for (int i = 1; i <= 10; i++)
            {
                pc.Consume();
                Thread.Sleep(500); // Biroz kutish
            }
        });

        // Thread'larni ishga tushirish
        producerThread.Start();
        consumerThread.Start();

        // Thread'lar tugaguncha kutish
        producerThread.Join();
        consumerThread.Join();

        Console.WriteLine("Hammasi bajarildi.");
    }
}