﻿using System;
using System.Collections.Generic;
using Telegram.Bot;
using Telegram.Bot.Args;
using System.Configuration;
using Telegram.Bot.Types.ReplyMarkups;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Telegram.Bot.Types;
using System.Collections;
using System.Diagnostics.Contracts;
using DRaumServerApp.telegram;
using Telegram.Bot.Types.Enums;


/*

Willkommen in der Kommentarspalte des Internets auf Telegram ;-)

=== BETA-PHASE, bitte keine weiteren Leute einladen! ===

Meine Idee:
Auf Twitter kann man gut Stimmungen einzelner Echokammern/Filterblasen verfolgen.
Das verzerrt aufgrund der eigenen Filterblase und durch Blockierungen jedoch das Gesamtbild.

Hier in diesem D-Raum auf Telegram sollen Meinungen aus verschiedenen 
Echokammern zusammentreffen und bewertet werden um ein übergreifendes Stimmungsbild zu zeigen. 
(Das D steht für vieles, suche dir was aus)

Damit dies gelingt, sind ein paar Regeln einzuhalten:

Die Texte hier sollen Kommentare sein, keine Kopien von Nachrichten im Netz (Urheberrecht und Sinn eines Meinungsforums).
Sie sollen nicht gegen Gesetze verstoßen, sonst wird der Kanal zu gemacht.
Keine offene Hetze oder persönliche Beleidigungen. Damit das auch eingehalten wird,
moderiere ich die Beiträge (nur freischalten oder mit Änderungen, die der Autor oder die Autorin bestätigen muss).

Damit keine Fan-Basis und Echokammern entstehen werden die Beiträge und die 
Bewertungen anonymisiert. Gegen Spam arbeitet im Hintergrund ein Bot-Programm, welches stetig 
verbessert wird. Zur Zeit ist eine Beitragsrate von 20 Minuten eingestellt. Wer etwas schreibt, bekommt den 
nächsten freien Veröffentlichungs-Slot zugeteilt und angezeigt.

Im Hauptkanal https://t.me/d_raum werden tagsüber die Beiträge veröffentlicht.
Jeden Tag werden die drei bestbewerteten Beiträge in dem Kanal https://t.me/d_raum_daily verlinkt. 
Einmal pro Woche werden die Top-10 Beiträge in dem Kanal https://t.me/d_raum_weekly verlinkt. 

Beiträge werden gelöscht, wenn sie entweder zu alt werden, viele negative Stimmen 
erhalten oder gegen Gesetze verstoßen. Abstimmen kann jeder mit 
einem Telegram-Account durch klick auf die Knöpfe unter den Beiträgen.

Selbst veröffentlichen, Feedback geben und weiteres kann man mit dem Eingabe-Bot @d_raum_input_bot

Viel Spaß,
Tom

*/

/*
 * 1 message per chat per second
 * 30 interactions per second per bot
 * 20 messages per minute per group
 */

namespace DRaumServerApp
{
  public class FileDateComparer : IComparer
  {
    public int Compare(object x, object y)
    {
      return ((FileInfo)x).LastWriteTime.CompareTo(((FileInfo)y).LastWriteTime);
    }
  }

  internal class DRaumManager
  {
    #region dynamicmembers
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    // D-Raum Daten
    private AuthorManager authors;
    private PostingManager posts;
    private DRaumStatistics statistics;
    private FeedbackManager feedbackManager;

    private readonly WorldInfoManager worldInfoManager = new WorldInfoManager();

    private readonly FeedbackBufferedSending feedbackBufferedSending;

    // Telegram Bots
    private readonly TelegramBotClient telegramInputBot;
    private readonly TelegramBotClient telegramPublishBot;
    private readonly TelegramBotClient telegramFeedbackBot;
    private readonly TelegramBotClient telegramModerateBot;
    private readonly TelegramBotClient telegramAdminBot;

    // Chats (mit dem Admin, Moderator, Feedback und zur Veröffentlichung in Kanälen)
    private readonly long feedbackChatId;
    private readonly long moderateChatId;
    private readonly long adminChatId;
    private readonly long draumChatId;
    private readonly long draumDailyChatId;
    private readonly long draumWeeklyChatId;

    private string adminStatisticText = "";
    private DateTime lastBackup = new DateTime(2000,1,1);

    #endregion

    // Statische Optionen
    private static readonly String backupFolder = "backups";
    private static readonly String filePrefix = "_draum_";

    private static readonly string roomname = "d_raum";

    private static readonly String writecommand = "schreiben";
    private static readonly String feedbackcommand = "nachricht";

    internal static readonly String modAcceptPrefix = "Y";
    private static readonly String modEditPrefix = "M";
    internal static readonly String modBlockPrefix = "N";
    internal static readonly String modGetNextCheckPostPrefix = "G";
    private static readonly String modDeletePrefix = "R";
    private static readonly String modClearFlagPrefix = "C";

    private static readonly String genericMessageDeletePrefix = "X";

    internal static readonly String voteUpPrefix = "U";
    internal static readonly String voteDownPrefix = "D";
    internal static readonly String flagPrefix = "F";

    private static readonly Telegram.Bot.Types.Enums.UpdateType[] receivefilterCallbackAndMessage = { Telegram.Bot.Types.Enums.UpdateType.CallbackQuery, Telegram.Bot.Types.Enums.UpdateType.Message };
    private static readonly Telegram.Bot.Types.Enums.UpdateType[] receivefilterCallbackOnly = { Telegram.Bot.Types.Enums.UpdateType.CallbackQuery, Telegram.Bot.Types.Enums.UpdateType.Message };

    private readonly CancellationTokenSource cancelTasksSource = new CancellationTokenSource();

    // Tasks und Intervalle für das regelmäßige Abarbeiten von Aufgaben
    private static readonly int intervalCheckPublishSeconds = 15;
    private static readonly int intervalBackUpDataMinutes = 60;
    private static readonly int intervalVoteAndFlagCountMinutes = 5;
    private static readonly int intervalpostcheckMilliseconds = 250;
    private static readonly int intervalStatisticCollectionMinutes = 60;

    private DateTime lastWorldNewsPost = new DateTime(1999, 1, 1, 9, 0, 0);
    private DateTime lastTopDaily = new DateTime(1999, 1, 1, 9, 0, 0);
    private DateTime lastTopWeekly = new DateTime(1999, 1, 1, 9, 0, 0);

    private readonly Task backupTask;
    private readonly Task publishTask;
    private readonly Task votingFlaggingTask;
    private readonly Task postAndFeedbackCheckingTask;
    private readonly Task statisticCollectTask;

    // Vorgefertigte Texte
    private static readonly String postIntro = "Die nächste Eingabe von Ihnen wird als Posting interpretiert. " +
      "Folgende Anforderungen sind zu erfüllen: \r\nTextlänge zwischen 100 und 1000\r\nKeine URLs\r\nKeine Schimpfworte und " +
      "ähnliches\r\nDer Text muss sich im Rahmen der Gesetze bewegen\r\nKeine Urheberrechtsverletzungen\r\n\r\nDer Text wird dann maschinell und ggf. durch " +
      "Menschen gegengelesen und wird bei eventuellen Anpassungen in diesem Chat zur Bestätigung durch Sie nochmal abgebildet. " +
      "Das Posting wird anonym veröffentlicht. Ihre User-ID wird intern gespeichert.";

    private static readonly String feedbackIntro = "Die nächste Eingabe von Ihnen wird als Feedback für Moderatoren und Kanalbetreiber weitergeleitet. " +
      "Folgende Anforderungen sind zu erfüllen: \r\nTextlänge zwischen 100 und 1000\r\nKeine URLs\r\nKeine Schimpfworte und " +
      "ähnliches. Ihre User-ID wird für eine eventuelle Rückmeldung gespeichert.";

    private static readonly String noModeChosen = "Willkommen. Es ist zur Zeit kein Modus gewählt! Mit /" + writecommand + " schaltet man in den Beitrag-Schreiben-Modus. Mit /" + 
                                                  feedbackcommand + " kann man " +  "in den Feedback-Modus gelangen und den Moderatoren und Kanalbetreibern eine Nachricht "+
                                                  "hinterlassen (Feedback/Wünsche/Kritik).";

    private static readonly String replyPost = "Danke für den Beitrag. Er wird geprüft und vor der Freigabe nochmal an Sie verschickt zum gegenlesen. Dies kann dauern, bitte Geduld.";

    private static readonly String replyFeedback = "Danke für das Feedback. Es wird nun von Moderatoren und Kanalbetreiber gelesen. Sie erhalten eventuell hier eine Rückmeldung.";

    internal DRaumManager()
    {
      this.telegramInputBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramInputToken"]);
      this.telegramPublishBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramPublishToken"]);
      this.telegramFeedbackBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramFeedbackToken"]);
      this.telegramModerateBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramModerateToken"]);
      this.telegramAdminBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramAdminToken"]);

      this.feedbackChatId = long.Parse(ConfigurationManager.AppSettings["feedbackChatID"]);
      this.moderateChatId = long.Parse(ConfigurationManager.AppSettings["moderateChatID"]);
      this.adminChatId = long.Parse(ConfigurationManager.AppSettings["adminChatID"]);
      this.draumChatId = long.Parse(ConfigurationManager.AppSettings["mainRoomID"]);
      this.draumDailyChatId = long.Parse(ConfigurationManager.AppSettings["dailyRoomID"]);
      this.draumWeeklyChatId = long.Parse(ConfigurationManager.AppSettings["weeklyRoomID"]);

      this.telegramAdminBot.SendTextMessageAsync(chatId: this.adminChatId, text: "Server ist gestartet");

      Task<Update[]> taskInput = this.telegramInputBot.GetUpdatesAsync();
      Task<Update[]> taskModerate = this.telegramModerateBot.GetUpdatesAsync();
      Task<Update[]> taskPublish = this.telegramPublishBot.GetUpdatesAsync();
      Task<Update[]> taskFeedback = this.telegramFeedbackBot.GetUpdatesAsync();

      logger.Info("Lade Autor-Manager");
      this.authors = new AuthorManager();
      logger.Info("Lade Posting-Manager");
      this.posts = new PostingManager();
      logger.Info("Lade Statistik-Manager");
      this.statistics = new DRaumStatistics();
      logger.Info("Lade Feedback-Manager");
      this.feedbackManager = new FeedbackManager();

      if(!this.loadDataFromFiles())
      {
        this.telegramAdminBot.SendTextMessageAsync(chatId: this.adminChatId, text: "!!! Server ist ohne Daten gestartet !!!");
        logger.Info("Lade Autor-Manager neu");
        this.authors = new AuthorManager();
        logger.Info("Lade Posting-Manager neu");
        this.posts = new PostingManager();
        logger.Info("Lade Statistik-Manager neu");
        this.statistics = new DRaumStatistics();
        logger.Info("Lade Feedback-Manager neu");
        this.feedbackManager = new FeedbackManager();
      }

      logger.Info("Setze das Offset bei den Nachrichten, um nicht erhaltene Updates zu löschen");
      Update[] updates = taskInput.Result;
      if (updates.Length > 0)
      {
        this.telegramInputBot.MessageOffset = updates[updates.Length - 1].Id + 1;
      }
      updates = taskModerate.Result;
      if (updates.Length > 0)
      {
        this.telegramModerateBot.MessageOffset = updates[updates.Length - 1].Id + 1;
      }
      updates = taskPublish.Result;
      if (updates.Length > 0)
      {
        this.telegramPublishBot.MessageOffset = updates[updates.Length - 1].Id + 1;
      }
      updates = taskFeedback.Result;
      if (updates.Length > 0)
      {
        this.telegramFeedbackBot.MessageOffset = updates[updates.Length - 1].Id + 1;
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

      this.feedbackBufferedSending = new FeedbackBufferedSending(this.feedbackManager, this.telegramFeedbackBot, this.feedbackChatId);

      logger.Info("Starte periodische Aufgaben");
      this.backupTask = this.periodicBackupTask(new TimeSpan(0, intervalBackUpDataMinutes, 0), this.cancelTasksSource.Token);
      this.publishTask = this.periodicPublishTask(new TimeSpan(0, 0, intervalCheckPublishSeconds), this.cancelTasksSource.Token);
      this.votingFlaggingTask = this.periodicVotingFlaggingTask(new TimeSpan(0, intervalVoteAndFlagCountMinutes,0), this.cancelTasksSource.Token);
      this.postAndFeedbackCheckingTask = this.periodicInputCheckTask(new TimeSpan(0, 0, 0, 0, intervalpostcheckMilliseconds), this.cancelTasksSource.Token);
      this.statisticCollectTask = this.periodicStatisticCollectTask(new TimeSpan(0, intervalStatisticCollectionMinutes, 0), this.cancelTasksSource.Token);
    }

    public async Task periodicVotingFlaggingTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Voting-Flagging-Task ist gestartet");
      while (true)
      {
        try
        {
          await Task.Delay(interval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }
       
        this.voteFlagTask();
        
      }
      logger.Info("Voting-Flagging-Task ist beendet");
    }

    public async Task periodicStatisticCollectTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Statistik-Task ist gestartet");
      while (true)
      {
        try
        {
          await Task.Delay(interval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }

        this.statisticCollectionTask();
      }
      logger.Info("Statistik-Task ist beendet");
    }

    public async Task periodicInputCheckTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Input-Check-Task ist gestartet");
      while (true)
      {
        try
        {
          await Task.Delay(interval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }

        this.inputCheckTask();
      }
      logger.Info("Input-Check-Task ist beendet");
    }

    public async Task periodicPublishTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Publish-Task ist gestartet");
      while (true)
      {
        try
        {
          await Task.Delay(interval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }

        this.publishingTask();
      }
      logger.Info("Publish-Task ist beendet");
    }

    public async Task periodicBackupTask(TimeSpan interval, CancellationToken cancellationToken)
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

    private async void checkAndUpdateAdminStatistic()
    {
      String newtext = "Interaktionen im letzten Intervall: " + this.statistics.getLastInteractionIntervalCount();
      newtext += "Letztes Backup: " + this.lastBackup.ToString(Utilities.usedCultureInfo);
      if (!newtext.Equals(this.adminStatisticText))
      {
        this.adminStatisticText = newtext;
        int msgId = this.feedbackManager.getAdminStatisticMessageID();
        if (msgId == -1)
        {
          Message msg = this.telegramAdminBot.SendTextMessageAsync(
            chatId: this.adminChatId,
            text: this.adminStatisticText).Result;
          this.feedbackManager.setAdminStatisticMessageID(msg.MessageId);
        }
        else
        {
          await this.telegramAdminBot.EditMessageTextAsync(
            chatId: this.adminChatId,
            messageId: msgId,
            text: this.adminStatisticText);
        }
      }
    }

    private void statisticCollectionTask()
    {
      // Run statistic updates
      try
      {
        this.statistics.switchInteractionInterval();
        checkAndUpdateAdminStatistic();
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler beim Verarbeiten der Statistik");
      }
    }

    private void inputCheckTask()
    {
      // Eingehende, zu moderierende Posts bearbeiten
      if (this.posts.getAndResetPostsCheckChangeFlag())
      {
        int messageID = this.feedbackManager.getModerateMessageID();
        int postsToCheck = this.posts.howManyPostsToCheck();
        String message = "Es gibt " + postsToCheck + " Posts zu moderieren.";
        if (messageID == -1)
        {
          // Gibt noch keine Moderator-Message, neu Anlegen
          try
          {
            Message msg = this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: message,
              replyMarkup: Keyboards.getGetNextPostToModerateKeyboard()).Result;
            this.feedbackManager.setModerateMessageID(msg.MessageId);
          }
          catch(Exception e)
          {
            logger.Error(e, "Fehler beim Anlegen der Moderations-Nachricht");
          }
        }
        else
        {
          // Update der Message
          try
          {
             _ = this.telegramModerateBot.EditMessageTextAsync (
              chatId: this.moderateChatId,
              messageId: messageID,
              text: message,
              replyMarkup: Keyboards.getGetNextPostToModerateKeyboard()).Result;
          }
          catch (Exception e)
          {
            logger.Error(e, "Fehler beim Aktualisieren der Moderations-Nachricht");
          }
        }
      }
      
    }

    private async void voteFlagTask()
    {
      // Posts prüfen, ob Buttons im Chat angepasst werden müssen
      IEnumerable<long> dirtyposts = this.posts.getDirtyPosts();
      foreach (long postID in dirtyposts)
      {
        logger.Debug("Buttons eines Posts werden aktualisiert");
        try
        {
          Message msg = this.telegramPublishBot.EditMessageReplyMarkupAsync(
            chatId: this.draumChatId,
            messageId: this.posts.getMessageID(postID),
            replyMarkup: Keyboards.getPostKeyboard(this.posts.getUpVotePercentage(postID), postID)).Result;
          this.posts.resetDirtyFlag(postID);
          await Task.Delay(3000);
        }
        catch (Exception ex)
        {
          logger.Error(ex, "Beim aktualisieren eines Buttons eines Beitrags trat ein Fehler auf.");
        }
      }

      // Posts prüfen, ob Texte im Chat angepasst werden müssen
      dirtyposts = this.posts.getTextDirtyPosts();
      foreach (long postID in dirtyposts)
      {
        logger.Debug("Text eines Posts wird aktualisiert");
        try
        {
          Message msg = this.telegramPublishBot.EditMessageTextAsync(
            chatId: this.draumChatId,
            parseMode: ParseMode.Html,
            replyMarkup: Keyboards.getPostKeyboard(this.posts.getUpVotePercentage(postID), postID),
            messageId: this.posts.getMessageID(postID),
            text: this.buildPostingText(postID)).Result;
          this.posts.resetTextDirtyFlag(postID);
          await Task.Delay(3000);
        }
        catch (Exception ex)
        {
          logger.Error(ex, "Beim aktualisieren eines Textes eines Beitrags trat ein Fehler auf.");
        }
      }



      // Prüfen, ob ein Flag vorliegt und dem Admin melden
      IEnumerable<long> flaggedPosts = this.posts.getFlaggedPosts();
      foreach (long postID in flaggedPosts)
      {
        // getText and send to Admin
        String msgText = "Dieser Post wurde geflaggt!!! \r\n" + this.posts.getPostingText(postID);
        // Tastatur für Admin: Flag verwerfen, Beitrag löschen
        InlineKeyboardButton deleteButton = InlineKeyboardButton.WithCallbackData("Beitrag löschen", modDeletePrefix + postID);
        InlineKeyboardButton clearFlagButton = InlineKeyboardButton.WithCallbackData("Flag entfernen", modClearFlagPrefix + postID);
        List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
        {
          deleteButton,
          clearFlagButton
        };
        InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(buttonlist);
        try
        {
          Message msg = this.telegramAdminBot.SendTextMessageAsync(
            chatId: this.adminChatId,
            text: msgText,
            replyMarkup: keyboard).Result;
        }
        catch (Exception ex)
        {
          logger.Error(ex, "Beim senden eines geflaggten Beitrags trat ein Fehler auf.");
        }
      }
      // Update the Status of top-posts
      this.posts.updateTopPostStatus(this.statistics);

    }
    
    private async Task backUpTask()
    {
      try
      {
        logger.Debug("Anhalten für Backup");
        this.telegramInputBot.StopReceiving();
        this.telegramModerateBot.StopReceiving();
        this.telegramPublishBot.StopReceiving();
        this.telegramFeedbackBot.StopReceiving();
        ManualResetEvent mre = new ManualResetEvent(false);
        SyncManager.halt(mre);
        if (!mre.WaitOne(TimeSpan.FromMinutes(3)))
        {
          logger.Error("Die Tasks sind nicht alle angehalten!");
        }
        await this.backupData();
        this.telegramInputBot.StartReceiving(receivefilterCallbackAndMessage);
        this.telegramModerateBot.StartReceiving(receivefilterCallbackAndMessage);
        this.telegramPublishBot.StartReceiving(receivefilterCallbackOnly);
        this.telegramFeedbackBot.StartReceiving(receivefilterCallbackAndMessage);
        SyncManager.unhalt();
        logger.Debug("Backup erledigt, weitermachen");
        this.lastBackup = DateTime.Now;
        this.checkAndUpdateAdminStatistic();
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler im Backup-Task");
      }
    }

    private async void publishingTask()
    {
      bool skip = false;
      // News-Post
      if( (DateTime.Now - this.lastWorldNewsPost).TotalHours > 24.0 )
      {
        if (this.lastWorldNewsPost.Year <= 2000)
        {
          skip = true;
        }
        this.lastWorldNewsPost = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 9, 0, 0);
        logger.Info("Nächste News am " + this.lastWorldNewsPost.AddHours(24).ToString(Utilities.usedCultureInfo));
        if (!skip)
        {
          String news = this.worldInfoManager.getInfoStringForChat();
          try
          {
            await this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumChatId,
              text: news);
          }
          catch (Exception e)
          {
            logger.Error(e, "Fehler beim Posten der News");
          }
        }
      }

      skip = false;
      // Top-Daily
      if ((DateTime.Now - this.lastTopDaily).TotalHours > 24.0)
      {
        if (this.lastTopDaily.Year <= 2000)
        {
          skip = true;
        }
        this.lastTopDaily = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 9, 0, 0);
        logger.Info("Nächste Top Tages Posts am " + this.lastTopDaily.AddHours(24).ToString(Utilities.usedCultureInfo));
        if (!skip)
        {
          List<long> topPosts = this.posts.getDailyTopPostsFromYesterday();
          foreach (long postId in topPosts)
          {
            logger.Debug("Es soll folgender Post in Top-Daily veröffentlicht werden: " + postId);
            Message result = this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumDailyChatId,
              parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
              text: this.buildPostingTextForTopTeaser(postId),
              replyMarkup: Keyboards.getTopPostLinkKeyboard(this.posts.getMessageID(postId), DRaumManager.roomname)
            ).Result;
            if (result == null || result.MessageId == 0)
            {
              logger.Error("Fehler beim Publizieren des Posts (daily,keine msg ID) bei Post " + postId);
            }
            else
            {
              this.posts.setDailyChatMsgId(postId, result.MessageId);
            }
            await Task.Delay(3000);
          }
        }
      }

      skip = false;
      // Top-Weekly
      if ((DateTime.Now - this.lastTopWeekly).TotalDays > 7.0)
      {

        DayOfWeek currentDay = DateTime.Now.DayOfWeek;
        int daysTillCurrentDay = currentDay - DayOfWeek.Saturday;
        if(daysTillCurrentDay < 0)
        {
          daysTillCurrentDay += 7;
        }
        DateTime currentWeekStartDate = DateTime.Now.AddDays(-daysTillCurrentDay);
        if (this.lastTopWeekly.Year <= 2000)
        {
          skip = true;
        }
        this.lastTopWeekly = new DateTime(currentWeekStartDate.Year, currentWeekStartDate.Month, currentWeekStartDate.Day, 9, 0, 0);
        logger.Info("Nächste Top Wochen Posts am " + this.lastTopWeekly.AddDays(7).ToString(Utilities.usedCultureInfo));
        if (!skip)
        {
          List<long> topPosts = this.posts.getWeeklyTopPostsFromLastWeek();
          foreach (long postId in topPosts)
          {
            logger.Debug("Es soll folgender Post in Top-Weekly veröffentlicht werden: " + postId);
            Message result = this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumWeeklyChatId,
              parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
              text: this.buildPostingTextForTopTeaser(postId),
              replyMarkup: Keyboards.getTopPostLinkKeyboard(this.posts.getMessageID(postId), DRaumManager.roomname)
            ).Result;
            if (result == null || result.MessageId == 0)
            {
              logger.Error("Fehler beim Publizieren des Posts (weekly,keine msg ID) bei Post " + postId);
            }
            else
            {
              this.posts.setWeeklyChatMsgId(postId, result.MessageId);
            }

            await Task.Delay(3000);
          }
        }
      }



      long postID = -1;
      bool fail = false;
      try
      {
        Posting toPublish = this.posts.tryPublish();
        if (toPublish != null)
        {
          postID = toPublish.getPostID();
          // Ab in den D-Raum damit
          logger.Debug("Es soll folgender Post veröffentlicht werden: " + postID);
          Message result = this.telegramPublishBot.SendTextMessageAsync(
            chatId: this.draumChatId,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            text: this.buildPostingText(postID),
            replyMarkup: Keyboards.getPostKeyboard(toPublish.getUpVotePercentage(),postID)
          ).Result;
          if (result == null || result.MessageId == 0)
          {
            logger.Error("Fehler beim Publizieren des Posts (keine msg ID) bei Post " + postID);
          }
          else
          {
            toPublish.resetTextDirtyFlag();
            toPublish.resetDirtyFlag();
            toPublish.setChatMessageID(result.MessageId);
          }
        }
      }
      catch (Exception e)
      {
        logger.Error(e, "(Exception)Fehler beim Publizieren des Posts: " + postID);
        await this.telegramAdminBot.SendTextMessageAsync(
          chatId: this.adminChatId,
          text: "Fehler beim Publizieren des Posts: " + postID);
        fail = true;
      }
      if (fail)
      {
        // TODO den Post wieder einreihen
      }
    }

    internal async void shutDown()
    {
      logger.Info("ShutDown läuft, Aufgaben werden abgebrochen, Listener werden beendet, Backup wird erstellt");
      this.cancelTasksSource.Cancel();
      this.feedbackBufferedSending.shutDownTask();
      try
      {
        await this.backupTask;
        await this.publishTask;
        await this.postAndFeedbackCheckingTask;
        await this.votingFlaggingTask;
        await this.statisticCollectTask;
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
      this.telegramAdminBot.SendTextMessageAsync(chatId: this.adminChatId, text: "Server ist beendet!").Wait();
      await this.backupData();
    }

    private String getDateFileString()
    {
      DateTime t = DateTime.Now;
      return t.Year+"_"+t.Month+"_"+t.Day+"_"+t.Hour + "_" + t.Minute;
    }

    internal bool loadDataFromFiles()
    {
      // Suche im Backup-Ordner nach dem neuesten Satz backupdateien
      String dateprefix = "";
      DirectoryInfo di = new DirectoryInfo(backupFolder);
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
            dateprefix = filelist[lastindex].Name.Substring(0, filelist[lastindex].Name.IndexOf(filePrefix, StringComparison.Ordinal));
            logger.Info("Lade die Daten aus diesen Dateien: " + filelist[lastindex].Name);
            if(lastindex != filelist.Length-1)
            {
              logger.Warn("Dies waren nicht die letzten Dateien im Verzeichnis!");
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
        inputFilestream = System.IO.File.OpenRead(backupFolder + Path.DirectorySeparatorChar + dateprefix + filePrefix + "authors.json");
        StreamReader sr = new StreamReader(inputFilestream);
        String jsonstring = sr.ReadToEnd();
        sr.Close();
        this.authors = JsonConvert.DeserializeObject<AuthorManager>(jsonstring);
        logger.Info("Lese Post-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(backupFolder + Path.DirectorySeparatorChar + dateprefix + filePrefix + "posts.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.posts = JsonConvert.DeserializeObject<PostingManager>(jsonstring);
        logger.Info("Lese Statistik-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(backupFolder + Path.DirectorySeparatorChar + dateprefix + filePrefix + "statistic.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.statistics = JsonConvert.DeserializeObject<DRaumStatistics>(jsonstring);
        logger.Info("Lese Feedback-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(backupFolder + Path.DirectorySeparatorChar + dateprefix + filePrefix + "feedback.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.feedbackManager = JsonConvert.DeserializeObject<FeedbackManager>(jsonstring);
        if (this.authors == null || this.posts == null || this.statistics == null || this.feedbackManager == null)
        {
          logger.Error("Beim Deserialisieren wurde eine Klasse als NULL deserialisiert!");
          return false;
        }
        return true;
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler beim Laden der Daten, es wird bei 0 begonnen");
      }
      finally
      {
        if(inputFilestream != null)
        {
          inputFilestream.Close();
        }
      }
      return false;
    }

    internal async Task backupData()
    {
      FileStream backupfile = null;
      try
      {
        DirectoryInfo di = new DirectoryInfo(backupFolder);
        if (!di.Exists)
        {
          di.Create();
        }
        string datestring = this.getDateFileString();
        logger.Info("Schreibe Post-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(backupFolder + Path.DirectorySeparatorChar + datestring + filePrefix + "posts.json");
        StreamWriter sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.posts,Formatting.Indented));
        sr.Close();
        logger.Info("Schreibe Autoren-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(backupFolder + Path.DirectorySeparatorChar + datestring + filePrefix + "authors.json");
        sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.authors, Formatting.Indented));
        sr.Close();
        logger.Info("Schreibe Statistik-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(backupFolder + Path.DirectorySeparatorChar + datestring + filePrefix + "statistic.json");
        sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.statistics, Formatting.Indented));
        sr.Close();
        logger.Info("Schreibe Feedback-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(backupFolder + Path.DirectorySeparatorChar + datestring + filePrefix + "feedback.json");
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
        if (backupfile != null)
        {
          backupfile.Close();
        }
      }
    }

    private String buildPostingText(long postingId)
    {
      string dspacepost = null;
      if (this.posts.isTopPost(postingId))
      {
        dspacepost += "<b>🔈 TOP-POST Nr. " + postingId + " 🔈</b>\r\n\r\n";
      }
      else
      {
        dspacepost += "<b>Post Nr. " + postingId + "</b>\r\n\r\n";
      }
      dspacepost += this.posts.getPostingText(postingId);
      dspacepost += "\r\n\r\n" + this.authors.getAuthorPostText(this.posts.getAuthorID(postingId));
      dspacepost += "\r\n" + this.posts.getPostingStatisticText(postingId);
      return dspacepost;
    }

    private String buildPostingTextForTopTeaser(long postingId)
    {
      string dspacepost = null;
      if (this.posts.isTopPost(postingId))
      {
        dspacepost += "<b>🔈 TOP-POST Nr. " + postingId + " 🔈</b>\r\n\r\n";
      }
      else
      {
        dspacepost += "<b>Post Nr. " + postingId + "</b>\r\n\r\n";
      }
      dspacepost += this.posts.getPostingText(postingId).Substring(0,60)+" [...]";
      dspacepost += "\r\n\r\n" + this.authors.getAuthorPostText(this.posts.getAuthorID(postingId));
      dspacepost += "\r\n" + this.posts.getPostingStatisticText(postingId);
      return dspacepost;
    }

    async Task<bool> acceptPostForPublishing(long postingID)
    {
      PostingPublishManager.publishHourType publishType = this.authors.getPublishType(this.posts.getAuthorID(postingID), this.statistics.getPremiumLevelCap());
      String result = "";
      if (publishType != PostingPublishManager.publishHourType.NONE)
      {
        result = this.posts.acceptPost(postingID, publishType);
      }
      if (!result.Equals(""))
      {
        String teaserText = this.posts.getPostingTeaser(postingID);
        long authorId = this.posts.getAuthorID(postingID);
        if (authorId != -1)
        {
          this.authors.publishedSuccessfully(authorId);
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: this.posts.getAuthorID(postingID),
            text: "Der Post ist zum Veröffentlichen freigegeben: " + result + "\r\n\r\nVorschau: " + teaserText
          );
        }
        else
        {
          InlineKeyboardButton gotItButton = InlineKeyboardButton.WithCallbackData("Verstanden", genericMessageDeletePrefix);
          List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
          {
            gotItButton
          };
          InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(buttonlist);
          await this.telegramModerateBot.SendTextMessageAsync(
            chatId: this.moderateChatId,
            text: "Konnte den Userchat zu folgender Posting-ID nicht erreichen (Posting wird aber veröffentlicht): " + postingID + " Textvorschau: " + this.posts.getPostingTeaser(postingID),
            replyMarkup: keyboard
          );
        }
      }
      else
      {
        InlineKeyboardButton gotItButton = InlineKeyboardButton.WithCallbackData("Verstanden", genericMessageDeletePrefix);
        List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
          {
            gotItButton
          };
        InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(buttonlist);
        await this.telegramAdminBot.SendTextMessageAsync(
          chatId: this.adminChatId,
          text: "Der Post " + postingID + " konnte nicht in die Liste zu veröffentlichender Posts eingefügt werden, FEHLER!",
          replyMarkup: keyboard
        );
        return false;
      }
      return true;
    }

    /// <summary>
    /// Dies ist der Eingabebot, welcher die Beiträge und Feedback der Nutzer annimmt. Er ist die Schnittstelle zwischen Nutzer und Dem Kanal.
    /// </summary>
    async void onInputBotMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        // Empfängerprüfung und Spam-Block
        // Dieser Block wirft auch eine Exception, wenn die maximale Nutzerzahl erreicht ist
        try
        {
          if (!this.authors.isCoolDownOver(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.DEFAULT))
          {
            TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.DEFAULT);
            await this.telegramInputBot.SendTextMessageAsync(
              chatId: e.Message.Chat,
              text: "(Spamvermeidung) Zeit bis zur nächsten Interaktion: " + coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)"
            );
            return;
          }
        }
        catch(DRaumException dre)
        {
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: e.Message.Chat,
            text: "Ein Fehler trat auf: " + dre.Message
          );
          return;
        }
        /////// ===   Bot-Commands   === //////
        // Gab es einen Behfehl, um Beiträge zu schreiben?
        if (e.Message.Text.Equals("/" + writecommand))
        {
          // INIT posting mode          
          if(!this.authors.isCoolDownOver(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.POSTING))
          {
            TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.POSTING);
            String msgCoolDownText = "(Spamvermeidung) Zeit bis zum nächsten Posting: " + coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)";
            if (coolDownTime.TotalMinutes > 180)
            {
              msgCoolDownText = "(Spamvermeidung) Zeit bis zum nächsten Posting: " + coolDownTime.TotalHours.ToString("0.0") + " Stunde(n)";
            }
            await this.telegramInputBot.SendTextMessageAsync(
              chatId: e.Message.Chat,
              text: msgCoolDownText
            );
            return;
          }
          this.statistics.increaseInteraction();
          this.authors.setPostMode(e.Message.From.Id, e.Message.From.Username);
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: e.Message.Chat,
            text: postIntro
          );
          return;
        }
        // Gab es einen Befehl, um Feedback abzugeben?
        if (e.Message.Text.Equals("/" + feedbackcommand))
        {
          // INIT feedback mode
          if (!this.authors.isCoolDownOver(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.FEEDBACK))
          {
            TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.FEEDBACK);
            String msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Feedbackmöglichkeit: " + coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)";
            if (coolDownTime.TotalMinutes > 180)
            {
              msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Feedbackmöglichkeit: " + coolDownTime.TotalHours.ToString("0.0") + " Stunde(n)";
            }
            await this.telegramInputBot.SendTextMessageAsync(
              chatId: e.Message.Chat,
              text: msgCoolDownText
            );
            return;
          }
          this.statistics.increaseInteraction();
          this.authors.setFeedbackMode(e.Message.From.Id, e.Message.From.Username);
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: e.Message.Chat,
            text: feedbackIntro
          );
          return;
        }
        /////// ===   Input-Processing    === ////////
        if (this.authors.isPostMode(e.Message.From.Id, e.Message.From.Username))
        {
          // == NEW POST SUBMITTED ==
          if (!SpamFilter.checkPostInput(e.Message.Text, out string posttext, out string message))
          {
            // spamfilter hat zugeschlagen
            await this.telegramInputBot.SendTextMessageAsync(
              chatId: e.Message.Chat,
              text: "Abgelehnt, Text ändern und erneut senden. Meldung des Spamfilters: " + message
            );
            return;
          }
          this.statistics.increaseInteraction();
          this.authors.resetCoolDown(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.POSTING);
          this.authors.resetCoolDown(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.DEFAULT);
          this.authors.unsetModes(e.Message.From.Id, e.Message.From.Username);
          this.posts.addPosting(posttext, e.Message.From.Id);
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: e.Message.Chat,
            text: replyPost + "\r\nMeldung des Spamfilters: " + message
          );
          return;
        }           
        if (this.authors.isFeedbackMode(e.Message.From.Id, e.Message.From.Username))
        {
          // == Feedback ==
          if (!SpamFilter.checkPostInput(e.Message.Text, out string feedbacktext, out string message))
          {
            // spamfilter hat zugeschlagen
            await this.telegramInputBot.SendTextMessageAsync(
              chatId: e.Message.Chat,
              text: "Abgelehnt, Text ändern und erneut senden. Meldung des Spamfilters: " + message
            );
            return;
          }
          this.statistics.increaseInteraction();
          this.authors.resetCoolDown(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.FEEDBACK);
          this.authors.resetCoolDown(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.DEFAULT);
          this.authors.unsetModes(e.Message.From.Id, e.Message.From.Username);
          this.feedbackManager.enqueueFeedback(new FeedbackElement( "Von: @" + e.Message.From.Username + " ID(" + e.Message.From.Id + ") : " + feedbacktext, e.Message.Chat.Id));        
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: e.Message.Chat,
            text: replyFeedback
          );
          return;
        }
        // Kein Modus
        await this.telegramInputBot.SendTextMessageAsync(
          chatId: e.Message.Chat,
          text: noModeChosen
        );
      }
    }

    async void onFeedbackCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        String callbackData = e.CallbackQuery.Data;
        String callbackAction = callbackData.Substring(0, 1);
        String chatidstring = callbackData.Substring(1);
        long chatID = 0;
        if (chatidstring.Trim().Length > 0)
        {
          chatID = long.Parse(chatidstring);
        }
        if (callbackAction.Equals(modBlockPrefix))
        {
          // verwerfen des feedbacks
          await this.telegramFeedbackBot.EditMessageReplyMarkupAsync(
           chatId: this.feedbackChatId,
           messageId: e.CallbackQuery.Message.MessageId,
           replyMarkup: null
          );
          return;
        }
        if(callbackAction.Equals(modAcceptPrefix))
        {
          // Antworten auf das Feedback
          this.feedbackManager.enableWaitForFeedbackReply(chatID);
          await this.telegramFeedbackBot.SendTextMessageAsync(
            chatId: this.feedbackChatId,
            text: "Der nächste eingegebene Text wird an den Autor gesendet"
          );
        }
      }
    }

    async void onAdminCallback(object sender, CallbackQueryEventArgs e)
    {
      if(e.CallbackQuery.Data != null)
      {
        string callbackData = e.CallbackQuery.Data;
        string callbackAction = callbackData.Substring(0, 1);
        string postidstring = callbackData.Substring(1);
        long postingID = 0;
        if (postidstring.Trim().Length > 0)
        {
          postingID = long.Parse(postidstring);
        }
        if (callbackAction.Equals(genericMessageDeletePrefix))
        {
          await this.telegramModerateBot.DeleteMessageAsync(
            chatId: this.adminChatId,
            e.CallbackQuery.Message.MessageId);
        }
        if(callbackAction.Equals(modDeletePrefix))
        {
          // Der Admin entscheided den geflaggten Post zu entfernen
          int messageID = this.posts.getMessageID(postingID);
          int messageIdDaily = this.posts.getMessageIdDaily(postingID);
          int messageIdWeekly = this.posts.getMessageIdWeekly(postingID);
          if (messageID != -1)
          {
            if (!this.posts.removePost(postingID))
            {
              logger.Error("Konnte den Post nicht aus dem Datensatz löschen");
            }
            try
            {
              // Nachricht aus dem D-Raum löschen
              await this.telegramPublishBot.DeleteMessageAsync(
                chatId: this.draumChatId,
                messageId: messageID);
              if (messageIdDaily != -1)
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumDailyChatId,
                  messageId: messageIdDaily);
              }
              if (messageIdWeekly != -1)
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumWeeklyChatId,
                  messageId: messageIdWeekly);
              }
              // Nachricht aus dem Admin-Chat löschen
              await this.telegramAdminBot.DeleteMessageAsync(
                chatId: this.adminChatId,
                messageId: e.CallbackQuery.Message.MessageId
              );
              // Rückmeldung an Admin
              await this.telegramAdminBot.AnswerCallbackQueryAsync(
                callbackQueryId: e.CallbackQuery.Id,
                text: "Beitrag wurde gelöscht",
                showAlert: true);
            }
            catch (Exception ex)
            {
              logger.Error(ex, "Konnte den Post nicht aus dem Kanal löschen: " + postingID);
            }
          }
          else
          {
            logger.Error("Es konnte keine Message-ID gefunden werden (im Chat) um den Beitrag zu löschen : " + postingID);
          }
        }
        if(callbackAction.Equals(modClearFlagPrefix))
        {
          // Der Admin entscheided, den Flag zurückzunehmen
          if (this.posts.removeFlagFromPost(postingID))
          {
            await this.telegramAdminBot.DeleteMessageAsync(
              chatId: this.adminChatId,
              messageId: e.CallbackQuery.Message.MessageId
            );
            await this.telegramAdminBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: "Flag wurde entfernt",
              showAlert: true);
          }
          else
          {
            logger.Error("Konnte das Flag vom Post nicht entfernen: " + postingID);
          }
        }
      }
    }

    /// <summary>
    /// Diese Funktion verarbeitet das Drücken der Knöpfe im Moderations-Bot
    /// </summary>
    async void onModerateCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        string callbackData = e.CallbackQuery.Data;
        string callbackAction = callbackData.Substring(0, 1);
        string postidstring = callbackData.Substring(1);
        long postingID = 0;
        if (postidstring.Trim().Length > 0)
        {
          postingID = long.Parse(postidstring);
        }
        
        
        // ==  Der Moderator akzeptiert den Beitrag
        if (callbackAction.Equals(modAcceptPrefix))
        {          
          if(this.acceptPostForPublishing(postingID).Result)
          {
            // Message not needed anymore, delete
            await this.telegramModerateBot.DeleteMessageAsync(
              chatId: this.moderateChatId,
              messageId: e.CallbackQuery.Message.MessageId
            );
            await this.telegramModerateBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: "Beitrag wird freigegeben",
              showAlert: true);
          }
          else
          {
            await this.telegramModerateBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: "Konnte den Post nicht freigeben...",
              showAlert: true);
          }
          return;
        }
        // ==  Der Moderator will den Beitrag bearbeiten und zurücksenden
        if (callbackAction.Equals(modEditPrefix))
        {
          // Anstelle der Moderieren-Button den Entfernen-Knopf einblenden
          InlineKeyboardButton gotItButton = InlineKeyboardButton.WithCallbackData("Entfernen", genericMessageDeletePrefix);
          List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
          {
            gotItButton
          };
          InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(buttonlist);
          await this.telegramModerateBot.EditMessageReplyMarkupAsync(
            chatId: this.moderateChatId,
            messageId: e.CallbackQuery.Message.MessageId,
            replyMarkup: keyboard
          );
          this.feedbackManager.waitForModerationText(postingID);
          await this.telegramModerateBot.AnswerCallbackQueryAsync(
            callbackQueryId: e.CallbackQuery.Id,
            text: "Editierten Beitrag abschicken",
            showAlert: true
          );
          return;
        }
        // ==  Der Moderator lehnt den Beitrag ab
        if (callbackAction.Equals(modBlockPrefix))
        {
          // Nachricht entfernen
          await this.telegramModerateBot.DeleteMessageAsync(
            chatId: this.moderateChatId,
            messageId: e.CallbackQuery.Message.MessageId
          );
          this.feedbackManager.waitForDenyingText(postingID);
          await this.telegramModerateBot.SendTextMessageAsync(
            chatId: this.moderateChatId,
            text: "Begründung schreiben und abschicken"
          );
          return;
        }
        if(callbackAction.Equals(genericMessageDeletePrefix))
        {
          await this.telegramModerateBot.DeleteMessageAsync(
            chatId: this.moderateChatId,
            e.CallbackQuery.Message.MessageId);
        }
        if(callbackAction.Equals(modGetNextCheckPostPrefix))
        {
          KeyValuePair<long, String> postingPair = this.posts.getNextPostToCheck();
          if (postingPair.Key != -1)
          {
            InlineKeyboardButton okayButton = InlineKeyboardButton.WithCallbackData("OK", modAcceptPrefix + postingPair.Key.ToString());
            InlineKeyboardButton modifyButton = InlineKeyboardButton.WithCallbackData("EDIT", modEditPrefix + postingPair.Key.ToString());
            InlineKeyboardButton blockButton = InlineKeyboardButton.WithCallbackData("BLOCK", modBlockPrefix + postingPair.Key.ToString());

            List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
            {
              okayButton,
              modifyButton,
              blockButton
            };
            InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(buttonlist);
            try
            {
              Message msg = this.telegramModerateBot.SendTextMessageAsync(
                chatId: this.moderateChatId,
                text: postingPair.Value,
                replyMarkup: keyboard
              ).Result;
            }
            catch (Exception ex)
            {
              logger.Error(ex, "Fehler beim Versenden der Moderationsprüfung von Post " + postingPair.Key + " wird zurück in die Schlange gestellt.");
              if (!this.posts.putBackIntoQueue(postingPair.Key))
              {
                logger.Error("Konnte den Post nicht wieder einfügen, wird gelöscht.");
                Posting posting = this.posts.removePostFromInCheck(postingPair.Key);
                if(posting != null)
                {
                  await this.telegramInputBot.SendTextMessageAsync(
                    chatId: posting.getAuthorID(),
                    text: "Dieser Beitrag konnte aufgrund interner Fehler nicht bearbeitet werden:  " +
                      posting.getPostingText() +
                      "\r\n\r\nBitte nochmal probieren. Sollte der Fehler weiterhin bestehen, bitte an einen Administrator wenden."
                    );
                }
                else
                {
                  logger.Error("Der Post konnte nicht gelöscht werden: " + postingPair.Key);
                }
              }
            }
          }
        }
      }
    }

    async void onPublishCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        // Auswerten: Vote-up, Vote-down, Flag
        string callbackData = e.CallbackQuery.Data;
        string callbackAction = callbackData.Substring(0, 1);
        string postidstring = callbackData.Substring(1);
        long postingID = 0;
        if (postidstring.Trim().Length > 0)
        {
          postingID = long.Parse(postidstring);
        }
        if (callbackAction.Equals(voteUpPrefix))
        {
          // UPVOTE
          String responseText = "Stimme bereits abgegeben oder eigener Post";
          if (this.posts.canUserVote(postingID, e.CallbackQuery.From.Id))
          {            
            int votecount = this.authors.voteUpAndGetCount(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username);
            if(votecount != 0)
            {
              this.statistics.increaseInteraction();
              this.authors.updateCredibility(this.posts.getAuthorID(postingID), votecount, 0);
              this.posts.upvote(postingID, e.CallbackQuery.From.Id, votecount);
              responseText = "Positivstimme erhalten";
            }
            else
            {
              responseText = "Fehler beim Abstimmen!";
            }            
          }
          await this.telegramPublishBot.AnswerCallbackQueryAsync(
             callbackQueryId: e.CallbackQuery.Id,
             text: responseText,
             showAlert: true
          );
        }
        if(callbackAction.Equals(voteDownPrefix))
        {
          // DOWNVOTE
          String responseText = "Stimme bereits abgegeben oder eigener Post";
          if (this.posts.canUserVote(postingID, e.CallbackQuery.From.Id))
          {
            int votecount = this.authors.voteDownAndGetCount(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username);
            if (votecount != 0)
            {
              this.statistics.increaseInteraction();
              this.authors.updateCredibility(this.posts.getAuthorID(postingID), 0, votecount);
              this.posts.downvote(postingID, e.CallbackQuery.From.Id, votecount);
              responseText = "Negativstimme erhalten";
            }
            else
            {
              responseText = "Fehler beim Abstimmen!";
            }
          }
          await this.telegramPublishBot.AnswerCallbackQueryAsync(
             callbackQueryId: e.CallbackQuery.Id,
             text: responseText,             
             showAlert: true
          );
        }
        if(callbackAction.Equals(flagPrefix))
        {
          // Flagging
          if (!this.authors.isCoolDownOver(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username, Author.InteractionCooldownTimer.FLAGGING))
          {
            TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username, Author.InteractionCooldownTimer.FLAGGING);
            String msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Markiermöglichkeit: " + coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)";
            if (coolDownTime.TotalMinutes > 180)
            {
              msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Markiermöglichkeit: " + coolDownTime.TotalHours.ToString("0.0") + " Stunde(n)";
            }
            await this.telegramInputBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: msgCoolDownText
            );
            return;
          }
          string responseText = "Beitrag bereits markiert oder eigener Post";
          if (this.posts.canUserFlag(postingID, e.CallbackQuery.From.Id))
          {
            this.statistics.increaseInteraction();
            this.authors.resetCoolDown(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username, Author.InteractionCooldownTimer.FLAGGING);
            this.posts.flag(postingID, e.CallbackQuery.From.Id);
            responseText = "Beitrag für Moderation markiert";           
          }
          await this.telegramPublishBot.AnswerCallbackQueryAsync(
            callbackQueryId: e.CallbackQuery.Id,
            text: responseText,
            showAlert: true
          );
        }
      }
    }

    /// <summary>
    /// Der Nutzer hat einen Beitrag mit Buttons bekommen und muss entscheiden, ob der Beitrag gepostet wird oder nicht
    /// </summary>
    async void onInputBotCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        string callbackData = e.CallbackQuery.Data;
        string callbackAction = callbackData.Substring(0, 1);
        string postidstring = callbackData.Substring(1);
        long postingID = 0;
        if (postidstring.Trim().Length > 0)
        {
          postingID = long.Parse(postidstring);
        }
        if (callbackAction.Equals(modAcceptPrefix))
        {
          if(this.acceptPostForPublishing(postingID).Result)
          {
            // Nachricht löschen
            await this.telegramInputBot.DeleteMessageAsync(
              chatId: e.CallbackQuery.Message.Chat.Id,
              messageId: e.CallbackQuery.Message.MessageId
            );
            await this.telegramInputBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: "Der Beitrag ist angenommen",
              showAlert: true);
          }
          else
          {
            await this.telegramInputBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: "Post konnte nicht veröffentlicht werden. Probieren Sie es nochmal. Falls es wiederholt fehlschlägt, wenden Sie sich an den Administrator.",
              showAlert: true);
          }
          return;
        }
        if (callbackAction.Equals(modBlockPrefix))
        {
          // Nachricht löschen
          await this.telegramInputBot.DeleteMessageAsync(
            chatId: e.CallbackQuery.Message.Chat.Id,
            messageId: e.CallbackQuery.Message.MessageId
          );
          this.posts.discardPost(postingID);
          await this.telegramInputBot.AnswerCallbackQueryAsync(
            callbackQueryId: e.CallbackQuery.Id,
            text: "Der Post wird nicht veröffentlicht und verworfen.",
            showAlert: true
          );
          return;
        }
      }
    }

    void onReceiveError(Object sender, ReceiveErrorEventArgs e)
    {
      logger.Error("Telegram.Bots .NET received an Exception: " + e.ApiRequestException.Message);
    }

    void onReceiveGeneralError(Object sender, ReceiveGeneralErrorEventArgs e)
    {
      logger.Error("Telegram.Bots .NET received a general Exception: " + e.Exception.Message);
    }

    async void onFeedbackMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        if (this.feedbackManager.isWaitingForFeedbackReply())
        {
          long chatId = this.feedbackManager.processFeedbackReplyAndGetChatID();
          await this.telegramInputBot.SendTextMessageAsync(
            chatId: chatId,
            text: "Eine Antwort des Kanalbetreibers auf Ihr Feedback:\r\n\r\n" + e.Message.Text
          );
          await this.telegramFeedbackBot.SendTextMessageAsync(
            chatId: this.feedbackChatId,
            text: "Feedback-Antwort ist verschickt"
          );
        }
      }
    }

    async void onModerateMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        if (this.feedbackManager.isWaitingForModeratedText())
        {
          // Den moderierten Text dem Nutzer zum bestätigen zuschicken.
          Posting posting = this.posts.getPostingInCheck(this.feedbackManager.getNextModeratedPostID());          
          if (posting != null)
          {
            posting.updateText(e.Message.Text);
            InlineKeyboardButton acceptButton = InlineKeyboardButton.WithCallbackData("Veröffentlichen", modAcceptPrefix + posting.getPostID().ToString());
            InlineKeyboardButton cancelButton = InlineKeyboardButton.WithCallbackData("Ablehnen", modBlockPrefix + posting.getPostID().ToString());
            List<InlineKeyboardButton> buttonlist = new List<InlineKeyboardButton>
            {
              acceptButton,
              cancelButton
            };
            InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(buttonlist);
            await this.telegramInputBot.SendTextMessageAsync(
              chatId: posting.getAuthorID(),
              text: posting.getPostingText(),
              replyMarkup: keyboard
            );
            this.feedbackManager.resetProcessModerationText();
            InlineKeyboardButton gotItButton = InlineKeyboardButton.WithCallbackData("Verstanden", genericMessageDeletePrefix);
            List<InlineKeyboardButton> buttonlist2 = new List<InlineKeyboardButton>
            {
              gotItButton
            };
            InlineKeyboardMarkup keyboard2 = new InlineKeyboardMarkup(buttonlist2);
            await this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: "Geänderter Text ist dem Autor zugestellt.",
              replyMarkup: keyboard2
            );
            await this.telegramModerateBot.DeleteMessageAsync(
              chatId: this.moderateChatId,
              messageId: e.Message.MessageId);
          }
          else
          {
            InlineKeyboardButton gotItButton = InlineKeyboardButton.WithCallbackData("Verstanden", genericMessageDeletePrefix);
            List<InlineKeyboardButton> buttonlist2 = new List<InlineKeyboardButton>
            {
              gotItButton
            };
            InlineKeyboardMarkup keyboard2 = new InlineKeyboardMarkup(buttonlist2);
            logger.Error("Konnte den zu editierenden Post nicht laden: " + this.feedbackManager.getNextModeratedPostID());
            await this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: "Der zu editierende Post wurde nicht gefunden. Nochmal den Text abschicken. Wenn der Fehler bestehen bleibt, einen Administrator informieren",
              replyMarkup: keyboard2
            );
          }
          return;
        }
        if (this.feedbackManager.isWaitingForDenyText())
        {
          // Die Begründung dem Nutzer zuschicken.
          long postingID = this.feedbackManager.getNextModeratedPostID();
          Posting posting = this.posts.getPostingInCheck(postingID);          
          if (posting != null)
          {
            String teaser = this.posts.getPostingTeaser(postingID);
            await this.telegramInputBot.SendTextMessageAsync(
              chatId: posting.getAuthorID(),
              text: "Der Beitrag wurde durch Moderation abgelehnt. Begründung:\r\n" + e.Message.Text + "\r\n\r\nBeitragsvorschau: " + teaser
            );
            this.feedbackManager.resetProcessModerationText();
            this.posts.discardPost(postingID);
          }
          return;
        }
      }
    }

  }
}
