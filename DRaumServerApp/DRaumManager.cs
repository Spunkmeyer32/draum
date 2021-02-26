using System;
using System.Collections.Generic;
using Telegram.Bot;
using Telegram.Bot.Args;
using System.Configuration;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using DRaumServerApp.Authors;
using DRaumServerApp.CyclicTasks;
using DRaumServerApp.Postings;
using DRaumServerApp.TelegramUtilities;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


namespace DRaumServerApp
{
  
  /// <summary>
  /// Hauptklasse für den D-Raum Telegram-Bot-Server
  /// </summary>
  internal class DRaumManager
  {
    #region dynamicmembers
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    // D-Raum Daten
    private AuthorManager authors;
    private PostingManager posts;
    private DRaumStatistics statistics;
    private FeedbackManager feedbackManager;
    private PostingTextBuilder textBuilder;

    private FeedbackSendingTask feedbackSendingTask;
    private PublishingTask publishingTask;
    private VoteAndFlagTask voteAndFlagTask;
    private StatisticCollectionTask statisticCollectionTask;
    private ModerationCheckTask moderationCheckTask;

    // Telegram Bots
    private TelegramBotClient telegramInputBot;
    private TelegramBotClient telegramPublishBot;
    private TelegramBotClient telegramFeedbackBot;
    private TelegramBotClient telegramModerateBot;
    private TelegramBotClient telegramAdminBot;

    private Bots.InputBot inputBot;
    private Bots.AdminBot adminBot;
    private Bots.ModerateBot moderateBot;
    private Bots.FeedbackBot feedbackBot;
    private Bots.PublishBot publishBot;
    
    private Task backupTask;

    private string startupinfo = "Keine Info";

    private readonly CancellationTokenSource cancelTasksSource = new CancellationTokenSource();

    #endregion

    // Statische Optionen
    private const string BackupFolder = "backups";
    private const string FilePrefix = "_draum_";
    internal const string Roomname = "d_raum";
    private const string Writecommand = "schreiben";
    private const string Feedbackcommand = "nachricht";

    private static readonly UpdateType[] receivefilterCallbackAndMessage = {UpdateType.CallbackQuery, UpdateType.Message };
    private static readonly UpdateType[] receivefilterCallbackOnly = {UpdateType.CallbackQuery, UpdateType.Message };
    
    // Tasks und Intervalle für das regelmäßige Abarbeiten von Aufgaben
    private static readonly int intervalBackUpDataMinutes = 60;
    
    // Vorgefertigte Texte
    internal static readonly string PostIntro = "Schreib-Modus!\r\n\r\nDie nächste Eingabe von Ihnen wird als Posting interpretiert. " +
                                                "Folgende Anforderungen sind zu erfüllen: \r\n\r\n▫️Textlänge zwischen 100 und 1500\r\n▫️Keine URLs\r\n▫️Keine Schimpfworte und " +
                                                "ähnliches\r\n▫️Der Text muss sich im Rahmen der Gesetze bewegen\r\n▫️Keine Urheberrechtsverletzungen\r\n\r\nDer Text wird dann maschinell und ggf. durch " +
                                                "Menschen gegengelesen und wird bei eventuellen Anpassungen in diesem Chat zur Bestätigung durch Sie nochmal abgebildet. " +
                                                "Das Posting wird anonym veröffentlicht. Ihre User-ID wird intern gespeichert.";

    internal static readonly string FeedbackIntro = "Feedback-Modus!\r\n\r\nDie nächste Eingabe von Ihnen wird als Feedback für Moderatoren und Kanalbetreiber weitergeleitet. " +
                                                    "Folgende Anforderungen sind zu erfüllen: \r\n\r\n▫️Textlänge zwischen 100 und 1500\r\n▫️Keine URLs\r\n▫️Keine Schimpfworte und " +
                                                    "ähnliches.\r\n\r\nIhre User-ID wird für eine eventuelle Rückmeldung gespeichert.";

    private static readonly string NoModeChosen = "Willkommen beim D-Raum-Input-Bot 🤖.\r\n\r\nEs ist zur Zeit kein Modus gewählt! Mit /" + Writecommand + " schaltet man in den Beitrag-Schreiben-Modus. Mit /" + Feedbackcommand + " kann man in den Feedback-Modus gelangen und den Moderatoren und Kanalbetreibern eine Nachricht "+
                                                  "hinterlassen (Feedback/Wünsche/Kritik).";

    internal static readonly string ReplyPost = "Danke für den Beitrag ✍️.\r\n\r\nEr wird geprüft und vor der Freigabe nochmal in diesem Chat an Sie verschickt zum gegenlesen. Dies kann dauern, bitte Geduld.";

    internal static readonly string ReplyFeedback = "Danke für das Feedback 👍.\r\n\r\nEs wird nun von Moderatoren und Kanalbetreiber gelesen. Sie erhalten eventuell hier in diesem Chat eine Rückmeldung.";
    
 

    internal static void checkForTestingMode()
    {
      string testmode = ConfigurationManager.AppSettings["runInTestMode"];
      if (testmode.Equals("true"))
      {
        Utilities.Runningintestmode = true;
      }
    }

    internal void initData()
    {
      logger.Info("Lade Autor-Manager");
      this.authors = new AuthorManager();
      logger.Info("Lade Posting-Manager");
      this.posts = new PostingManager();
      logger.Info("Lade Statistik-Manager");
      this.statistics = new DRaumStatistics();
      logger.Info("Lade Feedback-Manager");
      this.feedbackManager = new FeedbackManager();
      if (!this.loadDataFromFiles())
      {
        this.startupinfo = "!!! Server ist ohne Daten gestartet !!!";
        logger.Info("Lade Autor-Manager neu");
        this.authors = new AuthorManager();
        logger.Info("Lade Posting-Manager neu");
        this.posts = new PostingManager();
        logger.Info("Lade Statistik-Manager neu");
        this.statistics = new DRaumStatistics();
        logger.Info("Lade Feedback-Manager neu");
        this.feedbackManager = new FeedbackManager();
      }
      else
      {
        this.startupinfo = "Server ist gestartet";
      }
      this.textBuilder = new PostingTextBuilder(this.posts, this.authors);
      this.startupinfo += "\r\nMaximale Autorenzahl:" + AuthorManager.Maxmanagedusers;
    }

    internal async Task start()
    {
      this.telegramInputBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramInputToken"]);
      this.telegramPublishBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramPublishToken"]);
      this.telegramFeedbackBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramFeedbackToken"]);
      this.telegramModerateBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramModerateToken"]);
      this.telegramAdminBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramAdminToken"]);

      Task<Update[]> taskInput = this.telegramInputBot.GetUpdatesAsync();
      Task<Update[]> taskModerate = this.telegramModerateBot.GetUpdatesAsync();
      Task<Update[]> taskPublish = this.telegramPublishBot.GetUpdatesAsync();
      Task<Update[]> taskFeedback = this.telegramFeedbackBot.GetUpdatesAsync();

      this.inputBot = new Bots.InputBot(this.authors, this.statistics, this.telegramInputBot, this.posts, this.feedbackManager);
      this.adminBot = new Bots.AdminBot(this.telegramAdminBot);
      this.moderateBot = new Bots.ModerateBot(this.telegramModerateBot);
      this.feedbackBot = new Bots.FeedbackBot(this.telegramFeedbackBot);
      this.publishBot = new Bots.PublishBot(this.telegramPublishBot,this.posts, this.textBuilder);

      await this.adminBot.sendMessage(this.startupinfo +"\r\n" + this.statistics.getHardwareInfo());

      logger.Info("Setze das Offset bei den Nachrichten, um nicht erhaltene Updates zu löschen");
      Update[] updates = taskInput.Result;
      if (updates.Length > 0)
      {
        this.telegramInputBot.MessageOffset = updates[^1].Id + 1;
      }
      updates = taskModerate.Result;
      if (updates.Length > 0)
      {
        this.telegramModerateBot.MessageOffset = updates[^1].Id + 1;
      }
      updates = taskPublish.Result;
      if (updates.Length > 0)
      {
        this.telegramPublishBot.MessageOffset = updates[^1].Id + 1;
      }
      updates = taskFeedback.Result;
      if (updates.Length > 0)
      {
        this.telegramFeedbackBot.MessageOffset = updates[^1].Id + 1;
      }

      logger.Info("Beginne das Lauschen auf eingehende Nachrichten...");
      this.telegramInputBot.OnMessage += this.onInputBotMessage;
      this.telegramInputBot.OnCallbackQuery += this.onInputBotCallback;
      this.telegramInputBot.OnReceiveError += this.onReceiveError;
      this.telegramInputBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramInputBot.StartReceiving(receivefilterCallbackAndMessage);

      this.telegramModerateBot.OnCallbackQuery += this.onModerateCallback;
      this.telegramModerateBot.OnMessage += this.onModerateMessage;
      this.telegramModerateBot.OnReceiveError += this.onReceiveError;
      this.telegramModerateBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramModerateBot.StartReceiving(receivefilterCallbackAndMessage);

      this.telegramPublishBot.OnCallbackQuery += this.onPublishCallback;
      this.telegramPublishBot.OnReceiveError += this.onReceiveError;
      this.telegramPublishBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramPublishBot.StartReceiving(receivefilterCallbackOnly);

      this.telegramFeedbackBot.OnCallbackQuery += this.onFeedbackCallback;
      this.telegramFeedbackBot.OnMessage += this.onFeedbackMessage;
      this.telegramFeedbackBot.OnReceiveError += this.onReceiveError;
      this.telegramFeedbackBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramFeedbackBot.StartReceiving(receivefilterCallbackAndMessage);

      this.telegramAdminBot.OnCallbackQuery += this.onAdminCallback;
      this.telegramAdminBot.OnReceiveError += this.onReceiveError;
      this.telegramAdminBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramAdminBot.StartReceiving(receivefilterCallbackOnly);

      logger.Info("Starte periodische Aufgaben");
      this.feedbackSendingTask = new FeedbackSendingTask(this.feedbackManager, this.feedbackBot);
      this.publishingTask = new PublishingTask(this.publishBot, this.posts);
      this.voteAndFlagTask = new VoteAndFlagTask(this.posts, this.publishBot, this.statistics, this.adminBot);
      this.statisticCollectionTask = new StatisticCollectionTask(this.authors, this.statistics, this.posts, this.adminBot);
      this.moderationCheckTask = new ModerationCheckTask(this.posts, this.feedbackManager, this.moderateBot);

      this.backupTask = this.periodicBackupTask(new TimeSpan(0, intervalBackUpDataMinutes, 0), this.cancelTasksSource.Token);
    }

    private async Task periodicBackupTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Backup-Task ist gestartet");
      while (true)
      {
        try
        {
          await Task.Delay(interval, cancellationToken);
        }
        catch(OperationCanceledException)
        {
          break;
        }
        if(cancellationToken.IsCancellationRequested)
        {
          break;
        }
        await this.backUpTask();
      }

      logger.Info("Backup-Task ist beendet");
    }

    private async Task backUpTask()
    {
      try
      {
        logger.Info("Anhalten für Backup");
        this.telegramInputBot.StopReceiving();
        this.telegramModerateBot.StopReceiving();
        this.telegramPublishBot.StopReceiving();
        this.telegramFeedbackBot.StopReceiving();
        this.telegramAdminBot.StopReceiving();
        ManualResetEvent mre = new ManualResetEvent(false);
        SyncManager.halt(mre);
        if (!mre.WaitOne(TimeSpan.FromMinutes(3)))
        {
          logger.Error("Die Tasks sind nicht alle angehalten! Tasks: " + SyncManager.getRunningTaskCount());
        }
        await this.backupData();
        this.telegramInputBot.StartReceiving(receivefilterCallbackAndMessage);
        this.telegramModerateBot.StartReceiving(receivefilterCallbackAndMessage);
        this.telegramPublishBot.StartReceiving(receivefilterCallbackOnly);
        this.telegramFeedbackBot.StartReceiving(receivefilterCallbackAndMessage);
        this.telegramAdminBot.StartReceiving(receivefilterCallbackOnly);
        SyncManager.unhalt();
        logger.Info("Backup erledigt, weitermachen");
        this.statistics.setLastBackup(DateTime.Now);
        /// TODO Alte Backups löschen
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler im Backup-Task");
      }
    }


    // == Persistency
    private bool loadDataFromFiles()
    {
      // Suche im Backup-Ordner nach dem neuesten Satz backupdateien
      string dateprefix = "";
      DirectoryInfo di = new DirectoryInfo(BackupFolder);
      if(di.Exists)
      {
        FileInfo[] filelist = di.GetFiles();
        Array.Sort(filelist, new FileDateComparer());
        // Ist nun aufsteigend sortiert
        int lastindex = filelist.Length - 1;
        bool validFound = false;
        while(!validFound && lastindex >= 0)
        {
          // Es müssen vier gleiche Dateien vorhanden sein
          if( filelist[lastindex].Name.Substring(0, 18).Equals(filelist[lastindex - 1].Name.Substring(0, 18)) &&
              filelist[lastindex].Name.Substring(0, 18).Equals(filelist[lastindex - 2].Name.Substring(0, 18)) &&
              filelist[lastindex].Name.Substring(0, 18).Equals(filelist[lastindex - 3].Name.Substring(0, 18)) )
          {
            validFound = true;
            dateprefix = filelist[lastindex].Name.Substring(0, filelist[lastindex].Name.IndexOf(FilePrefix, StringComparison.Ordinal));
            logger.Info("Lade die Daten aus diesen Dateien: " + filelist[lastindex].Name);
            if(lastindex != filelist.Length-1)
            {
              logger.Warn("Dies waren nicht die letzten Dateien im Verzeichnis: " + filelist[lastindex].Name);
            }
          }
          lastindex--;
        }
        if(!validFound)
        {
          return false;
        }        
      }

      FileStream inputFilestream = null;
      try
      {
        logger.Info("Lese Autoren-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(BackupFolder + Path.DirectorySeparatorChar + dateprefix + FilePrefix + "authors.json");
        StreamReader sr = new StreamReader(inputFilestream);
        string jsonstring = sr.ReadToEnd();
        sr.Close();
        this.authors = JsonConvert.DeserializeObject<AuthorManager>(jsonstring);
        logger.Info("Lese Post-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(BackupFolder + Path.DirectorySeparatorChar + dateprefix + FilePrefix + "posts.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.posts = JsonConvert.DeserializeObject<PostingManager>(jsonstring);
        logger.Info("Lese Statistik-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(BackupFolder + Path.DirectorySeparatorChar + dateprefix + FilePrefix + "statistic.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.statistics = JsonConvert.DeserializeObject<DRaumStatistics>(jsonstring);
        logger.Info("Lese Feedback-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(BackupFolder + Path.DirectorySeparatorChar + dateprefix + FilePrefix + "feedback.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.feedbackManager = JsonConvert.DeserializeObject<FeedbackManager>(jsonstring);
        if (this.authors == null || this.posts == null || this.statistics == null || this.feedbackManager == null)
        {
          logger.Error("Beim Deserialisieren wurde eine Klasse als NULL deserialisiert!");
          return false;
        }
        this.posts.transferFromInCheckToToCheck();
        return true;
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler beim Laden der Daten, es wird bei 0 begonnen");
      }
      finally
      {
        inputFilestream?.Close();
      }
      return false;
    }

    private async Task backupData()
    {
      FileStream backupfile = null;
      try
      {
        DirectoryInfo di = new DirectoryInfo(BackupFolder);
        if (!di.Exists)
        {
          di.Create();
        }
        string datestring = getDateFileString();
        logger.Info("Schreibe Post-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(BackupFolder + Path.DirectorySeparatorChar + datestring + FilePrefix + "posts.json");
        StreamWriter sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.posts, Formatting.Indented));
        sr.Close();
        logger.Info("Schreibe Autoren-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(BackupFolder + Path.DirectorySeparatorChar + datestring + FilePrefix + "authors.json");
        sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.authors, Formatting.Indented));
        sr.Close();
        logger.Info("Schreibe Statistik-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(BackupFolder + Path.DirectorySeparatorChar + datestring + FilePrefix + "statistic.json");
        sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.statistics, Formatting.Indented));
        sr.Close();
        logger.Info("Schreibe Feedback-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(BackupFolder + Path.DirectorySeparatorChar + datestring + FilePrefix + "feedback.json");
        sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.feedbackManager, Formatting.Indented));
        sr.Close();
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler beim Schreiben der Daten");
      }
      finally
      {
        backupfile?.Close();
      }
    }


    // == Methods

    internal async Task shutDown()
    {
      logger.Info("ShutDown läuft, Aufgaben werden abgebrochen, Listener werden beendet, Backup wird erstellt");
      this.cancelTasksSource.Cancel();
      try
      {
        await this.feedbackSendingTask.shutDownTask();
        await this.publishingTask.shutDownTask();
        await this.voteAndFlagTask.shutDownTask();
        await this.statisticCollectionTask.shutDownTask();
        await this.moderationCheckTask.shutDownTask();
        await this.backupTask;
      }
      catch (OperationCanceledException e)
      {
        logger.Warn($"{nameof(OperationCanceledException)} erhalten mit der Nachricht: {e.Message}");
      }
      finally
      {
        this.cancelTasksSource.Dispose();
      }

      logger.Info("Listener werden beendet, Backup wird erstellt");
      this.telegramInputBot.StopReceiving();
      this.telegramModerateBot.StopReceiving();
      this.telegramPublishBot.StopReceiving();
      this.telegramFeedbackBot.StopReceiving();
      this.telegramAdminBot.StopReceiving();

      await this.adminBot.sendMessage("Server ist beendet!");
      await this.backupData();

    }

    private void onReceiveError(object sender, ReceiveErrorEventArgs e)
    {
      logger.Error("Telegram.Bots .NET received an Exception: " + e.ApiRequestException.Message);
    }

    private void onReceiveGeneralError(object sender, ReceiveGeneralErrorEventArgs e)
    {
      logger.Error("Telegram.Bots .NET received a general Exception: " + e.Exception.Message);
    }

    

    private bool canUserVote(long postingId, long authorId, string authorName)
    {
      // Wenn der Nutzer Autor ist, kann er nicht voten
      if (!Utilities.Runningintestmode)
      {
        if (this.posts.isAuthor(postingId, authorId))
        {
          return false;
        }
      }
      return this.authors.canUserVote(postingId, authorId, authorName);
    }

    private bool canUserFlag(long postingId, long authorId, string authorName)
    {
      // Wenn der Nutzer Autor ist, kann er nicht voten
      if (!Utilities.Runningintestmode)
      {
        if (this.posts.isAuthor(postingId, authorId))
        {
          return false;
        }
      }
      return this.authors.canUserFlag(postingId, authorId,authorName);
    }

    private void upvote(long postingId, long authorId, int voteCount)
    {
      this.authors.vote(postingId, authorId);
      this.posts.upvote(postingId, voteCount);
    }

    private void downvote(long postingId, long authorId, int voteCount)
    {
      this.authors.vote(postingId, authorId);
      this.posts.downvote(postingId, voteCount);
    }

    private void flag(long postingId, long authorId)
    {
      this.authors.flag(postingId,authorId);
      this.posts.flag(postingId);
    }

    private static string getDateFileString()
    {
      DateTime t = DateTime.Now;
      return t.Year+"_"+t.Month+"_"+t.Day+"_"+t.Hour + "_" + t.Minute;
    }
    
    private async Task<bool> acceptPostForPublishing(long postingId)
    {
      PostingPublishManager.PublishHourType publishType = this.authors.getPublishType(this.posts.getAuthorId(postingId), this.statistics.getPremiumLevelCap());
      string result = "";
      if (publishType != PostingPublishManager.PublishHourType.None)
      {
        result = this.posts.acceptPost(postingId, publishType);
      }
      if (!result.Equals(""))
      {
        string teaserText = this.posts.getPostingTeaser(postingId);
        long authorId = this.posts.getAuthorId(postingId);
        if (authorId != -1)
        {
          this.authors.publishedSuccessfully(authorId);
          await this.inputBot.sendMessage(authorId, "Der Post ist zum Veröffentlichen freigegeben: " + result + "\r\n\r\nVorschau: " + teaserText);
        }
        else
        {
          await this.moderateBot.sendMessageWithKeyboard(
              "Konnte den Userchat zu folgender Posting-ID nicht erreichen (Posting wird aber veröffentlicht): " +
              postingId + " Textvorschau: " + this.posts.getPostingTeaser(postingId),
              Keyboards.getGotItDeleteButtonKeyboard(),false);
          return false;
        }
      }
      else
      {
        await this.adminBot.sendMessageWithKeyboard(
          "Der Post " + postingId + " konnte nicht in die Liste zu veröffentlichender Posts eingefügt werden, FEHLER!", Keyboards.getGotItDeleteButtonKeyboard());
        return false;
      }
      return true;
    }


    // == == CALLBACKS == ==

    private async void onFeedbackCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        if (callbackData.getPrefix().Equals(Keyboards.ModBlockPrefix))
        {
          // verwerfen des feedbacks
          await this.feedbackBot.removeInlineMarkup(e.CallbackQuery.Message.MessageId);
          return;
        }
        if(callbackData.getPrefix().Equals(Keyboards.ModAcceptPrefix))
        {
          // Antworten auf das Feedback
          this.feedbackManager.enableWaitForFeedbackReply(callbackData.getId());
          await this.feedbackBot.sendMessage("Der nächste eingegebene Text wird an den Autor gesendet");
        }
      }
    }

    private async void onAdminCallback(object sender, CallbackQueryEventArgs e)
    {
      if(e.CallbackQuery.Data != null)
      {
        DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        if (callbackData.getPrefix().Equals(Keyboards.GenericMessageDeletePrefix))
        {
          await this.adminBot.removeMessage(e.CallbackQuery.Message.MessageId);
          return;
        }
        if(callbackData.getPrefix().Equals(Keyboards.ModDeleteFlaggedPrefix))
        {
          // Der Admin entscheided den geflaggten Post zu entfernen
          string resultText = await this.publishBot.removePostingFromChannels(callbackData.getId());
          // Nachricht aus dem Admin-Chat löschen
          await this.adminBot.removeMessage(e.CallbackQuery.Message.MessageId);
          // Rückmeldung an Admin
          await this.adminBot.sendMessageWithKeyboard(resultText, Keyboards.getGotItDeleteButtonKeyboard());
          return;
        }
        if(callbackData.getPrefix().Equals(Keyboards.ModClearFlagPrefix))
        {
          // Der Admin entscheided, den Flag zurückzunehmen
          if (this.posts.removeFlagFromPost(callbackData.getId()))
          {
            await this.adminBot.removeMessage(e.CallbackQuery.Message.MessageId);
            await this.adminBot.replyToCallback(e.CallbackQuery.Id, "Flag wurde entfernt");
          }
          else
          {
            logger.Error("Konnte das Flag vom Post nicht entfernen: " + callbackData.getId());
          }
        }
      }
    }

    /// <summary>
    /// Diese Funktion verarbeitet das Drücken der Knöpfe im Moderations-Bot
    /// </summary>
    private async void onModerateCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data == null)
      {
        return;
      }
      DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
      // ==  Der Moderator akzeptiert den Beitrag
      if (callbackData.getPrefix().Equals(Keyboards.ModAcceptPrefix))
      {          
        if(this.acceptPostForPublishing(callbackData.getId()).Result)
        {
          // Message not needed anymore, delete
          await this.moderateBot.removeMessage(e.CallbackQuery.Message.MessageId);
          await this.moderateBot.replyToCallback(e.CallbackQuery.Id, "Beitrag wird freigegeben");
        }
        else
        {
          await this.moderateBot.replyToCallback(e.CallbackQuery.Id, "Konnte den Post nicht freigeben...");
        }
        return;
      }
      // ==  Der Moderator will den Beitrag bearbeiten und zurücksenden
      if (callbackData.getPrefix().Equals(Keyboards.ModEditPrefix))
      {
        await this.moderateBot.editMessageButtons(e.CallbackQuery.Message.MessageId,
          Keyboards.getGotItDeleteButtonKeyboard());
        this.feedbackManager.waitForModerationText(callbackData.getId());
        await this.moderateBot.replyToCallback(e.CallbackQuery.Id, "Editierten Beitrag abschicken");
        return;
      }
      // ==  Der Moderator lehnt den Beitrag ab
      if (callbackData.getPrefix().Equals(Keyboards.ModBlockPrefix))
      {
        // Nachricht entfernen
        await this.moderateBot.removeMessage(e.CallbackQuery.Message.MessageId);
        this.feedbackManager.waitForDenyingText(callbackData.getId());
        await this.moderateBot.sendMessageWithKeyboard("Begründung schreiben und abschicken",
          Keyboards.getGotItDeleteButtonKeyboard(),false);
        return;
      }
      // TODO Der Moderator blockt den Nutzer für einen Tag/ für eine Woche/ für einen Monat
      if(callbackData.getPrefix().Equals(Keyboards.GenericMessageDeletePrefix))
      {
        await this.moderateBot.removeMessage(e.CallbackQuery.Message.MessageId);
        return;
      }
      if(callbackData.getPrefix().Equals(Keyboards.ModGetNextCheckPostPrefix))
      {
        KeyValuePair<long, string> postingPair = this.posts.getNextPostToCheck();
        if (postingPair.Key == -1)
        {
          return;
        }
        Message msg = await this.moderateBot.sendMessageWithKeyboard(postingPair.Value,
          Keyboards.getModeratePostKeyboard(postingPair.Key), true);
        if (msg != null)
        {
          return;
        }
        // keine Nachricht, wieder neu einreihen
        if (this.posts.putBackIntoQueue(postingPair.Key))
        {
          return;
        }
        logger.Error("Konnte den Post nicht wieder einfügen, wird gelöscht:" + postingPair.Key + " TEXT:" + postingPair.Value);
        Posting posting = this.posts.removePostFromInCheck(postingPair.Key);
        if(posting != null)
        {
          await this.inputBot.sendMessage(posting.getAuthorId(),
            "Dieser Beitrag konnte aufgrund interner Fehler nicht bearbeitet werden:  " +
            posting.getPostingText() +
            "\r\n\r\nBitte nochmal probieren. Sollte der Fehler weiterhin bestehen, bitte an einen Administrator wenden.");
        }
        else
        {
          logger.Error("Der Post konnte nicht gelöscht werden: " + postingPair.Key);
        }
      }
    }

    private async void onPublishCallback(object sender, CallbackQueryEventArgs e)
    {
      try
      {
        if (e.CallbackQuery.Data != null)
        {
          // Auswerten: Vote-up, Vote-down, Flag
          DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
          if (callbackData.getPrefix().Equals(Keyboards.VoteUpPrefix))
          {
            // UPVOTE
            string responseText = "Stimme bereits abgegeben oder eigener Post";
            if (this.canUserVote(callbackData.getId(), e.CallbackQuery.From.Id, e.CallbackQuery.From.Username))
            {
              int votecount = this.authors.voteUpAndGetCount(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username);
              if (votecount != 0)
              {
                this.statistics.increaseInteraction();
                this.authors.updateCredibility(this.posts.getAuthorId(callbackData.getId()), votecount, 0);
                this.upvote(callbackData.getId(), e.CallbackQuery.From.Id, votecount);
                responseText = "Positivstimme erhalten";
              }
              else
              {
                responseText = "Fehler beim Abstimmen!";
              }
            }

            await this.publishBot.answerCallback(e.CallbackQuery.Id, responseText);
            return;
          }
          if (callbackData.getPrefix().Equals(Keyboards.VoteDownPrefix))
          {
            // DOWNVOTE
            string responseText = "Stimme bereits abgegeben oder eigener Post";
            if (this.canUserVote(callbackData.getId(), e.CallbackQuery.From.Id, e.CallbackQuery.From.Username))
            {
              int votecount = this.authors.voteDownAndGetCount(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username);
              if (votecount != 0)
              {
                this.statistics.increaseInteraction();
                this.authors.updateCredibility(this.posts.getAuthorId(callbackData.getId()), 0, votecount);
                this.downvote(callbackData.getId(), e.CallbackQuery.From.Id, votecount);
                responseText = "Negativstimme erhalten";
              }
              else
              {
                responseText = "Fehler beim Abstimmen!";
              }
            }
            await this.publishBot.answerCallback(e.CallbackQuery.Id, responseText);
            return;
          }
          if (callbackData.getPrefix().Equals(Keyboards.FlagPrefix))
          {
            // Flagging
            if (!this.authors.isCoolDownOver(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username, Author.InteractionCooldownTimer.Flagging))
            {
              TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.CallbackQuery.From.Id,
                e.CallbackQuery.From.Username, Author.InteractionCooldownTimer.Flagging);
              string msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Markiermöglichkeit: " +
                                       coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)";
              if (coolDownTime.TotalMinutes > 180)
              {
                msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Markiermöglichkeit: " +
                                  coolDownTime.TotalHours.ToString("0.0") + " Stunde(n)";
              }
              await this.publishBot.answerCallback(e.CallbackQuery.Id, msgCoolDownText);
              return;
            }
            string responseText = "Beitrag bereits markiert oder eigener Post";
            if (this.canUserFlag(callbackData.getId(), e.CallbackQuery.From.Id, e.CallbackQuery.From.Username))
            {
              this.statistics.increaseInteraction();
              this.authors.resetCoolDown(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username, Author.InteractionCooldownTimer.Flagging);
              this.flag(callbackData.getId(), e.CallbackQuery.From.Id);
              responseText = "Beitrag für Moderation markiert";
            }
            await this.publishBot.answerCallback(e.CallbackQuery.Id, responseText);
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Genereller Fehler");
      }
    }

    /// <summary>
    /// Der Nutzer hat einen Beitrag mit Buttons bekommen und muss entscheiden, ob der Beitrag gepostet wird oder nicht
    /// </summary>
    private async void onInputBotCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        if (callbackData.getPrefix().Equals(Keyboards.ModAcceptPrefix))
        {
          if(this.acceptPostForPublishing(callbackData.getId()).Result)
          {
            await this.inputBot.sendMessage(e.CallbackQuery.From.Id, "Der Beitrag ist angenommen");
            await this.inputBot.removeMessage(e.CallbackQuery.Message.MessageId, e.CallbackQuery.From.Id);
          }
          else
          {
            await this.inputBot.sendMessage(e.CallbackQuery.From.Id, 
              "Post konnte nicht veröffentlicht werden. Probieren Sie es nochmal. Falls es wiederholt fehlschlägt, wenden Sie sich an den Administrator.");
          }
          return;
        }
        if (callbackData.getPrefix().Equals(Keyboards.ModBlockPrefix))
        {
          if (!this.posts.removePost(callbackData.getId()))
          {
            logger.Error("Konnte den Post nicht aus dem Datensatz löschen: " + callbackData.getId());
          }
          await this.inputBot.removeMessage(e.CallbackQuery.Message.MessageId, e.CallbackQuery.From.Id);
          await this.inputBot.sendMessage(e.CallbackQuery.From.Id, "Der Post wird nicht veröffentlicht und verworfen.");
          return;
        }
        if (callbackData.getPrefix().Equals(Keyboards.ModeWritePrefix))
        {
          await this.inputBot.switchToWriteMode(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username,
            e.CallbackQuery.From.Id);
          return;
        }
        if (callbackData.getPrefix().Equals(Keyboards.ModeFeedbackPrefix))
        {
          await this.inputBot.switchToFeedbackMode(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username,
            e.CallbackQuery.From.Id);
        }
      }
    }


    // == == ON MESSAGES == ==

    /// <summary>
    /// Dies ist der Eingabebot, welcher die Beiträge und Feedback der Nutzer annimmt. Er ist die Schnittstelle zwischen Nutzer und Dem Kanal.
    /// </summary>
    private async void onInputBotMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        // Empfängerprüfung und Spam-Block
        // Dieser Block wirft auch eine Exception, wenn die maximale Nutzerzahl erreicht ist
        try
        {
          if (!this.authors.isCoolDownOver(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.Default))
          {
            TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.Default);
            await this.inputBot.sendMessage(e.Message.From.Id, 
              "⏳ (Spamvermeidung) Zeit bis zur nächsten Bot-Interaktion: " + coolDownTime.TotalMinutes.ToString("0.0") +
              " Minute(n)");
            return;
          }
        }
        catch(DRaumException dre)
        {
          await this.inputBot.sendMessage(e.Message.From.Id, "Ein Fehler trat auf: " + dre.Message);
          return;
        }
        if (e.Message.Text.Equals("/" + Writecommand))
        {
          await this.inputBot.switchToWriteMode(e.Message.From.Id, e.Message.From.Username, e.Message.Chat.Id);
          return;
        }
        if (e.Message.Text.Equals("/" + Feedbackcommand))
        {
          await this.inputBot.switchToFeedbackMode(e.Message.From.Id, e.Message.From.Username, e.Message.Chat.Id);
          return;
        }
        if (this.authors.isFeedbackMode(e.Message.From.Id, e.Message.From.Username) ||
            this.authors.isPostMode(e.Message.From.Id, e.Message.From.Username))
        {
          string text = Utilities.telegramEntitiesToHtml(e.Message.Text, e.Message.Entities);
          await this.inputBot.processTextInput(e.Message.From.Id, e.Message.From.Username, e.Message.Chat.Id, text);
        }
        else
        {
          // Kein Modus
          await this.inputBot.sendMessageWithKeyboard(e.Message.From.Id, NoModeChosen, Keyboards.getChooseInputModeKeyboard());
        }
      }
    }

    private async void onFeedbackMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        if (this.feedbackManager.isWaitingForFeedbackReply())
        {
          long chatId = this.feedbackManager.processFeedbackReplyAndGetChatId();
          await this.inputBot.sendMessage(chatId,
            "Eine Antwort des Kanalbetreibers auf Ihr Feedback:\r\n\r\n" + e.Message.Text);
          await this.feedbackBot.sendMessage("Feedback-Antwort ist verschickt");
        }
      }
    }

    private async void onModerateMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        if (this.feedbackManager.isWaitingForModeratedText())
        {
          // Den moderierten Text dem Nutzer zum bestätigen zuschicken.
          Posting posting = this.posts.getPostingInCheck(this.feedbackManager.getNextModeratedPostId());          
          if (posting != null)
          {
            posting.updateText(e.Message.Text, true);
            await this.inputBot.sendMessageWithKeyboard(posting.getAuthorId(), "MODERIERTER TEXT:\r\n\r\n"+posting.getPostingText(), Keyboards.getAcceptDeclineModeratedPostKeyboard(posting.getPostId()));
            this.feedbackManager.resetProcessModerationText();
            await this.moderateBot.sendMessageWithKeyboard(
              "Geänderter Text ist dem Autor zugestellt.", Keyboards.getGotItDeleteButtonKeyboard(),
              false);
            await this.moderateBot.removeMessage(e.Message.MessageId);
          }
          else
          {
            logger.Error("Konnte den zu editierenden Post nicht laden: " + this.feedbackManager.getNextModeratedPostId());
            await this.moderateBot.sendMessageWithKeyboard(
              "Der zu editierende Post wurde nicht gefunden. Nochmal den Text abschicken. Wenn der Fehler bestehen bleibt, einen Administrator informieren", 
              Keyboards.getGotItDeleteButtonKeyboard(),
              false);
          }
          return;
        }
        if (this.feedbackManager.isWaitingForDenyText())
        {
          // Die Begründung dem Nutzer zuschicken.
          long postingId = this.feedbackManager.getNextModeratedPostId();
          Posting posting = this.posts.getPostingInCheck(postingId);          
          if (posting != null)
          {
            string teaser = this.posts.getPostingTeaser(postingId);
            await this.inputBot.sendMessage(posting.getAuthorId(),
              "Der Beitrag wurde durch Moderation abgelehnt. Begründung:\r\n" +
              e.Message.Text + "\r\n\r\nBeitragsvorschau: " + teaser);
            this.feedbackManager.resetProcessModerationText();
            this.posts.discardPost(postingId);
          }
        }
      }
    }


  }
}
