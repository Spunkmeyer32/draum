using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Telegram.Bot.Types;
using static DRaumServerApp.Author;

namespace DRaumServerApp
{
  internal class AuthorManager
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    public static int Maxmanagedusers = 100; // int.max

    [JsonProperty]
    private ConcurrentDictionary<long, Author> authors;    

    internal AuthorManager()
    {
      if (Utilities.RUNNINGINTESTMODE)
      {
        Author.COOLDOWNMINUTES = 1;
        Author.COOLDOWNMINUTESFLAGGING = 1;
        Author.COOLDOWNHOURSPOSTING = 0;
        Author.COOLDOWNHOURSFEEDBACK = 0;
        AuthorManager.Maxmanagedusers = int.MaxValue;
      }
      this.authors = new ConcurrentDictionary<long, Author>();
    }

    internal AuthorManager(IDictionary<long,Author> authors)
    {
      this.authors = new ConcurrentDictionary<long, Author>();
      foreach(KeyValuePair<long,Author> pair in authors)
      {
        if(!this.authors.TryAdd(pair.Key, pair.Value))
        {
          logger.Error("Konnte folgende Nutzerdaten nicht in die Liste einfügen: " + pair.Key + " , " + pair.Value);
        }
      }
    }

    private Author getAuthor(long authorId)
    {
      if (this.authors.ContainsKey(authorId))
      {
        return this.authors[authorId];
      }
      return null;
    }

    internal bool canUserVote(long postingid, long authorId)
    {
      if (this.authors.ContainsKey(authorId))
      {
        return this.authors[authorId].canVote(postingid);
      }
      return false;
    }

    internal bool canUserFlag(long postingid, long authorId)
    {
      if (this.authors.ContainsKey(authorId))
      {
        return this.authors[authorId].canFlag(postingid);
      }
      return false;
    }

    private Author getAuthor(long authorId, string externalName) 
    {
      if (this.authors.ContainsKey(authorId))
      {
        return this.authors[authorId];
      }
      else
      {
        if (this.authors.Count < Maxmanagedusers)
        {
          if (externalName == null)
          {
            externalName = "";
          }
          Author newAuthor = new Author(authorId, externalName);
          if (this.authors.TryAdd(authorId, newAuthor))
          {
            logger.Info("Neuer Autor: " + externalName + " (" + authorId + ")");
            return newAuthor;
          }
          else
          {
            throw new DRaumException("Aufgrund eines Fehlers konnte der Nutzer nicht hinzugefügt werden. Bitter später erneut probieren oder einen Administrator kontaktieren.");
          }
        }
        else
        {
          throw new DRaumException("Maximale Nutzeranzahl erreicht");
        }
      }
    }

    internal void getMedianAndTopLevel(out int medianOut, out int topOut)
    {
      // alle Autoren Prüfen und Median und Top ermitteln
      int toplevel = 0;
      ArrayList levellist = new ArrayList();
      int temp;
      foreach(Author author in this.authors.Values)
      {
        temp = author.getLevel();
        levellist.Add(temp);
        if(temp > toplevel)
        {
          toplevel = temp;
        }
      }
      Array target = levellist.ToArray();
      Array.Sort(target);
      if(target.Length == 0)
      {
        medianOut = 0;
        topOut = 0;
        return;
      }
      int median = (int)target.GetValue(target.Length / 2);
      medianOut = median;
      topOut = toplevel;      
    }

    internal void setPostMode(long id, string externalName)
    {
      Author author = this.getAuthor(id, externalName);
      author.setPostMode();
    }

    internal PostingPublishManager.publishHourType getPublishType(long authorId, int premiumLevelCap)
    {
      Author author = this.getAuthor(authorId);
      if(author != null)
      {
        return author.getPublishType(premiumLevelCap);
      }
      return PostingPublishManager.publishHourType.NONE;
    }

    internal bool isPostMode(long id, string externalName)
    {
      Author author = this.getAuthor(id, externalName);
      return author.isInPostMode();
    }

    internal void unsetModes(long id, string externalName)
    {
      Author author = this.getAuthor(id, externalName);
      author.unsetModes();
    }

    internal void setFeedbackMode(long id, string externalName)
    {
      Author author = this.getAuthor(id, externalName);
      author.setFeedbackMode();
    }

    internal bool isFeedbackMode(long id, string externalName)
    {
      Author author = this.getAuthor(id, externalName);
      return author.isInFeedbackMode();
    }

    internal bool isCoolDownOver(long id, string externalName, InteractionCooldownTimer timerType)
    {
      Author author = this.getAuthor(id, externalName);
      return author.coolDownOver(timerType);
    }

    internal void resetCoolDown(long id, string externalName, InteractionCooldownTimer timerType)
    {
      Author author = this.getAuthor(id, externalName);
      author.resetCoolDown(timerType);
    }

    internal TimeSpan getCoolDownTimer(long id, string externalName, InteractionCooldownTimer timerType)
    {
      Author author = this.getAuthor(id, externalName);
      return author.getCoolDownTimer(timerType);
    }

    internal string getAuthorPostText(long authorId)
    {
      Author author = this.getAuthor(authorId);
      if(author == null)
      {
        return "<i>Schreiber/in nicht gefunden!</i>\r\n<i>Verfasst im D-Raum https://t.me/d_raum </i>";
      }
      return "<i>" + author.getShortAuthorInfo() + "</i>\r\n<i>Verfasst im D-Raum https://t.me/d_raum </i>";
    }

    internal void vote(long postingId, long authorId)
    {
      if (this.authors.ContainsKey(authorId))
      {
        this.authors[authorId].vote(postingId);
      }
    }

    internal void flag(long postingId, long authorId)
    {
      if (this.authors.ContainsKey(authorId))
      {
        this.authors[authorId].flag(postingId);
      }
    }


    internal void updateCredibility(long authorId, long receivedUpVotes, long receivedDownVotes)
    {
      Author author = this.getAuthor(authorId);
      if (author != null)
      {
        author.updateCredibility(receivedUpVotes, receivedDownVotes);
      }
      else
      {
        logger.Warn("Konnte votes dem Nutzer mit der ID " + authorId + " nicht zuordnen");
      }
    }

    internal void publishedSuccessfully(long authorId)
    {
      Author author = this.getAuthor(authorId);
      if (author != null)
      {
        author.publishedSuccessfully();
      }
      else
      {
        logger.Warn("Konnte die Veröffentlichung dem Nutzer mit der ID " + authorId + " nicht gutschreiben");
      }
    }

    internal int voteUpAndGetCount(long authorId, string username)
    {
      Author author = this.getAuthor(authorId, username);
      if (author != null)
      {
        return author.voteUpAndGetCount();
      }
      return 0;
    }

    internal int voteDownAndGetCount(long authorId, string username)
    {
      Author author = this.getAuthor(authorId,username);
      if (author != null)
      {
        return author.voteDownAndGetCount();
      }
      return 0;
    }

   
  }
    
}
