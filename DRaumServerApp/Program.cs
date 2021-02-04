using System;
using System.Diagnostics;
using System.Threading;
using System.Timers;

namespace DRaumServerApp
{
  class Program
  {

    static System.Timers.Timer timer = new System.Timers.Timer(1000) { AutoReset = true };
    static AutoResetEvent autoResetEvent = new AutoResetEvent(false);
    static bool stopApp = false;

    private static void Timer_Elapsed(object sender, ElapsedEventArgs e)
    {
      autoResetEvent.Set();
    }

    static void Main(string[] args)
    {
      var logger = NLog.LogManager.GetCurrentClassLogger();
      logger.Info("D-Raum-Server startet");
      try
      {
        DRaumManager dRaumManager = new DRaumManager();
        logger.Info("D-Raum-Server läuft");
        timer.Elapsed += Timer_Elapsed;
        timer.Start();
        Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
        {          
          logger.Info("D-Raum-Server endet");
          dRaumManager.shutDown();
          stopApp = true;
          logger.Info("D-Raum-Server ist beendet");
          e.Cancel = true;
        };
        
        while(true)
        {
          if(!autoResetEvent.WaitOne(3000))
          {
            logger.Warn("Der Warte-Timer der Haupt-Schleife wurde nicht benachrichtigt, Flaschenhals??");
          }
          if(stopApp)
          {
            break;
          }
        }
      }
      catch(Exception ex)
      {
        logger.Error(ex, "Unbehandelter Fehler im Programm");
      }
      finally
      {
        NLog.LogManager.Shutdown();
      }      
    }
  }
}
