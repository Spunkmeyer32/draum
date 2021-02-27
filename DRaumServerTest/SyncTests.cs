using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DRaumServerApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRaumServerTest
{
  [TestClass]
  public class SyncTests
  {

    [TestMethod]
    public void syncManagerTest()
    {
      bool running = true;
      int v1 = 1;
      int v2 = 1;
      int v3 = 1;

      CancellationTokenSource cts = new CancellationTokenSource();
      
      Task.Run(async () => 
      {
        SyncManager.register();
        while (running)
        {
          v1 = 0;
          await SyncManager.tryRunAfter(TimeSpan.FromMilliseconds(5), "test1", cts.Token);
          v1 = 1;
          Thread.Sleep(400);
          Console.WriteLine("Task 1");
          await Console.Out.FlushAsync();
        }

        SyncManager.unregister();
      }, cts.Token);

      Task.Run(async () =>
      {
        SyncManager.register();
        while (running)
        {
          v2 = 0;
          await SyncManager.tryRunAfter(TimeSpan.FromMilliseconds(5), "test2", cts.Token);
          v2 = 1;
          Thread.Sleep(300);
          Console.WriteLine("Task  2");
          await Console.Out.FlushAsync();
        }

        SyncManager.unregister();
      }, cts.Token);

      Task.Run(async () =>
      {
        SyncManager.register();
        while (running)
        {
          v3 = 0;
          await SyncManager.tryRunAfter(TimeSpan.FromMilliseconds(5), "test3", cts.Token);
          v3 = 1;
          Thread.Sleep(200);
          Console.WriteLine("Task   3");
          await Console.Out.FlushAsync();
        }

        SyncManager.unregister();
      }, cts.Token);

      Thread.Sleep(1000);
      ManualResetEvent allHalted = new ManualResetEvent(false);
      SyncManager.halt(allHalted);
      Console.WriteLine(DateTime.Now.Millisecond + " halt issued");
      Console.Out.Flush();
      if (!allHalted.WaitOne(700))
      {
        Assert.Fail("Needed to call cancel");
        cts.Cancel();
      }

      Assert.AreEqual(0, v1 + v2 + v3);
      Thread.Sleep(50);
      Assert.AreEqual(0, v1 + v2 + v3);
      Thread.Sleep(300);
      Assert.AreEqual(0, v1 + v2 + v3);
      Console.WriteLine(DateTime.Now.Millisecond + " all halted");
      Console.Out.Flush();
      SyncManager.unhalt();
      Thread.Sleep(500);

      running = false;
    }


  }

  
}