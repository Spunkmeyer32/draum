using System;
using System.Threading;
using System.Threading.Tasks;

namespace DRaumServerApp
{
  /// <summary>
  /// Um zyklische Tasks synchron anzuhalten ohne sie zu beenden kann diese statische Klasse verwendet werden.
  /// </summary>
  internal static class SyncManager
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    private static volatile int _runningTasks;
    private static readonly object tasksmutex = new object();
    private static volatile bool _shouldWait;
    private static readonly ManualResetEvent resetEvent = new ManualResetEvent(false);
    private static ManualResetEvent _externalEvent;


    /// <summary>
    /// Bevor der Task seine Arbeit verrichtet, sollte er diese Funktion aufrufen, welche eventuell zu synchronisationszwecken blockt
    /// und vor der Rückkehr einen Delay benutzt
    /// </summary>
    /// <param name="cancelToken">zum Abbrechen des Delays oder der Synchronisierung</param>
    /// <param name="delay">Zeit, nach der die Methode zurückkehrt</param>
    internal static async Task tryRunAfter(TimeSpan delay, CancellationToken cancelToken)
    {
      bool throwCancel = false;
      lock (tasksmutex)
      {
        _runningTasks -= 1;
      }
      try
      {
        await Task.Delay(delay, cancelToken);
      }
      catch (OperationCanceledException)
      {
        // continue
        throwCancel = true;
      }
      if (!_shouldWait)
      {
        lock (tasksmutex)
        {
          _runningTasks += 1;
        }

        if (throwCancel)
        {
          throw new OperationCanceledException();
        }
        return;
      }
      lock (tasksmutex)
      {
        if (_runningTasks == 0)
        {
          logger.Info("Alle Tasks haben gehalten");
          if (_externalEvent != null)
          {
            _externalEvent.Set();
          }
          else
          {
            unhalt();
          }
        }
        else
        {
          logger.Info("Jetzt sind noch " + _runningTasks + " Tasks aktiv");
        }
      }
      // Auf alle anderen Tasks warten
      while (!cancelToken.IsCancellationRequested)
      {
        if (resetEvent.WaitOne(300))
        {
          break;
        }
      }
      // Weiter
      lock (tasksmutex)
      {
        _runningTasks += 1;
      }
      if (throwCancel)
      {
        throw new OperationCanceledException();
      }
    }

    /// <summary>
    /// Alle Tasks werden angehalten und der Aufrufer wird über das Reset-Event-Signal informiert
    /// </summary>
    /// <param name="extEvnt"></param>
    internal static void halt(ManualResetEvent extEvnt)
    {
      lock (tasksmutex)
      {
        logger.Info(_runningTasks + " Tasks werden angehalten");
      }
      _externalEvent = extEvnt;
      // signal deaktivieren, um tasks am weiterlaufen zu hindern
      resetEvent.Reset();
      _shouldWait = true;
    }

    /// <summary>
    /// Alle Tasks weiterführen
    /// </summary>
    internal static void unhalt()
    {
      logger.Info("Tasks werden fortgeführt");
      _shouldWait = false;
      _externalEvent = null;
      // Dieses Signal wird alle weiterlaufen lassen:
      resetEvent.Set();
    }

    /// <summary>
    /// Tasks müssen sich selbst registrieren, um gezählt zu werden
    /// </summary>
    internal static void register()
    {
      lock (tasksmutex)
      {
        _runningTasks += 1;
        logger.Info("Nun " + _runningTasks + " Tasks registriert");
      }
    }

    /// <summary>
    /// Wenn ein Task beendet wird, muss er sich abmelden
    /// </summary>
    internal static void unregister()
    {
      lock (tasksmutex)
      {
        _runningTasks -= 1;
        logger.Info("Nun laufen noch " + _runningTasks + " Tasks (Andere sind beendet oder sind noch im Delay)");
      }
    }

    internal static int getRunningTaskCount()
    {
      lock (tasksmutex)
      {
        return _runningTasks;
      }
    }

  }
}