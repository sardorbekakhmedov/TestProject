namespace ConsoleApp1;

public partial class ProducerConsumer
{ 
    private readonly Queue<int> queue = new Queue<int>(); // Elementlarni saqlovchi navbat
    private readonly object locker = new object();        // Sinxronizatsiya uchun obyekt
    private const int MAX = 5;                            // Navbatdagi maksimal elementlar soni

    // 🟢 Producer metodi — yangi element qo‘shadi
    public void Produce(int item)
    {
        lock (locker)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            while (queue.Count >= MAX)
            {
                Console.WriteLine($"[Producer {threadId}] kutmoqda: navbat to‘la ({queue.Count})");
                Monitor.Wait(locker);
            }

            queue.Enqueue(item);
            Console.WriteLine($"[Producer {threadId}] 🛠️ Produced: {item}");

            Monitor.Pulse(locker);
        }
    }

    
    // 🔵 Consumer metodi — elementni oladi
    public void Consume()
    {
        lock (locker)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            while (queue.Count == 0)
            {
                Console.WriteLine($"[Consumer {threadId}] kutmoqda: navbat bo‘sh");
                Monitor.Wait(locker);
            }

            int item = queue.Dequeue();
            Console.WriteLine($"[Consumer {threadId}] ✅ Consumed: {item}");

            Monitor.Pulse(locker);
        }
    }
}