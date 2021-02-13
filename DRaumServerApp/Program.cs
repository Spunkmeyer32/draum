using System;
using System.Threading;
using System.Timers;

namespace DRaumServerApp
{
  internal class Program
  {

    private static readonly System.Timers.Timer timer = new System.Timers.Timer(1000) { AutoReset = true };
    private static readonly AutoResetEvent autoResetEvent = new AutoResetEvent(false);
    private static bool _stopApp;
    private static bool _sigintRec;

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
          _sigintRec = true;
          e.Cancel = true;
          logger.Info("D-Raum-Server endet (SIGINT)");
          dRaumManager.shutDown();
          _stopApp = true;
          logger.Info("D-Raum-Server ist beendet");
        };
        AppDomain.CurrentDomain.ProcessExit += (sender, eargs) =>
        {
          if (!_sigintRec)
          {
            logger.Info("D-Raum-Server endet (SIGTERM)");
            dRaumManager.shutDown();
            _stopApp = true;
            logger.Info("D-Raum-Server ist beendet");
          }
          else
          {
            logger.Info("SIGTERM ignoriert, da SIGINT bekommen");
          }
        };
        while (true)
        {
          if(!autoResetEvent.WaitOne(3000))
          {
            logger.Warn("Der Warte-Timer der Haupt-Schleife wurde nicht benachrichtigt, Flaschenhals??");
          }
          if(_stopApp)
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
