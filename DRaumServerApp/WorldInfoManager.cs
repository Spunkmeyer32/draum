﻿using RestSharp;
using System;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Configuration;

namespace DRaumServerApp
{
  class WorldInfoManager
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private float goldPriceInEuro;
    private float silverPriceInEuro;
    private float unleadedFuelPriceInEuro;
    private float unleadedBioFuelPriceInEuro;
    private float dieselFuelPriceInEuro;
    private float bitcoinInEuro;

    private const string TrendUp = "↗️";
    private const string TrendDown = "↘️";
    private const string TrendEqual = "➡️";

    private string goldPriceInEuroTrend = TrendEqual;
    private string silverPriceInEuroTrend = TrendEqual;
    private string unleadedFuelPriceInEuroTrend = TrendEqual;
    private string unleadedBioFuelPriceInEuroTrend = TrendEqual;
    private string dieselFuelPriceInEuroTrend = TrendEqual;
    private string bitcoinInEuroTrend = TrendEqual;

    private string infoString = "";
    private DateTime lastCheck = DateTime.Now;
    private static readonly CultureInfo currencyCulture = Utilities.UsedCultureInfo;


    public string getInfoStringForChatAsHtml(bool retrieveFromWeb)
    {
      if (!retrieveFromWeb && ((DateTime.Now - this.lastCheck).TotalHours < 24.0) && this.infoString.Length >= 10 )
      {
        return this.infoString;
      }
      this.lastCheck = DateTime.Now;
      // Daten einholen und in infoString speichern
      try
      {
        if (!Utilities.Runningintestmode)
        {
          this.getBitcoin();
          this.getFuelprice();
          this.getGold();
          this.getSilver();
        }

        if (this.infoString.Length < 10)
        {
          // Keine alten Daten, keine Trends anzeigen
          this.infoString = "== Tagesinfo ==\r\n\r\n🟡 Gold:  <code>" + this.goldPriceInEuro.ToString("C", currencyCulture) + "</code>\r\n" +
                            "⚪️ Silber:  <code>" + this.silverPriceInEuro.ToString("C", currencyCulture) + "</code>\r\n" +
                            "⛽️ Super-Benzin:  <code>" + this.unleadedFuelPriceInEuro.ToString("C", currencyCulture) + "</code>\r\n" +
                            "⛽️ Super-E10:  <code>" + this.unleadedBioFuelPriceInEuro.ToString("C", currencyCulture) + "</code>\r\n" +
                            "⛽️ Diesel:  <code>" + this.dieselFuelPriceInEuro.ToString("C", currencyCulture) + "</code>\r\n" +
                            "💰 Bitcoin:  <code>" + this.bitcoinInEuro.ToString("C", currencyCulture) + "</code>\r\n\r\n" +
                            "📅 Aktualisierung am <code>" + this.lastCheck.ToShortDateString() + "</code> um <code>" + this.lastCheck.ToShortTimeString() + "</code> Uhr";
        }
        else
        {
          this.infoString = "== Tagesinfo ==\r\n\r\n🟡 Gold:  <code>" + this.goldPriceInEuro.ToString("C", currencyCulture) + "  " + this.goldPriceInEuroTrend + "</code>\r\n" +
                            "⚪️ Silber:  <code>" + this.silverPriceInEuro.ToString("C", currencyCulture) + "  " + this.silverPriceInEuroTrend + "</code>\r\n" +
                            "⛽️ Super-Benzin:  <code>" + this.unleadedFuelPriceInEuro.ToString("C", currencyCulture) + "  " + this.unleadedFuelPriceInEuroTrend + "</code>\r\n" +
                            "⛽️ Super-E10:  <code>" + this.unleadedBioFuelPriceInEuro.ToString("C", currencyCulture) + "  " + this.unleadedBioFuelPriceInEuroTrend + "</code>\r\n" +
                            "⛽️ Diesel:  <code>" + this.dieselFuelPriceInEuro.ToString("C", currencyCulture) + "  " + this.dieselFuelPriceInEuroTrend + "</code>\r\n" +
                            "💰 Bitcoin:  <code>" + this.bitcoinInEuro.ToString("C", currencyCulture) + "  " + this.bitcoinInEuroTrend + "</code>\r\n\r\n" +
                            "📅 Aktualisierung am <code>" + this.lastCheck.ToShortDateString() + "</code> um <code>" + this.lastCheck.ToShortTimeString() + "</code> Uhr";
        }
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler beim holen der Informationen der WEB-APIs");
      }
      return this.infoString;
    }


    private void getGold()
    {
      var client = new RestClient("https://www.goldapi.io/api/XAU/EUR");
      var request = new RestRequest("", Method.Get);
      request.AddHeader("x-access-token", ConfigurationManager.AppSettings["infoAPIKeyGold"] );
      request.AddHeader("Content-Type", "application/json");
      RestResponse response = client.ExecuteGetAsync(request).Result;
      JObject jsonResponse = JObject.Parse(response.Content);
      try
      {
        float temp = jsonResponse.GetValue("price").ToObject<float>();
        if (this.goldPriceInEuro < temp)
        {
          this.goldPriceInEuroTrend = TrendUp;
        }
        else
        {
          if (this.goldPriceInEuro > temp)
          {
            this.goldPriceInEuroTrend = TrendDown;
          }
          else
          {
            this.goldPriceInEuroTrend = TrendEqual;
          }
        }
        this.goldPriceInEuro = temp;
      }
      catch (NullReferenceException nre)
      {
        logger.Error(nre, "Null beim Abfragen des Goldpreises");
      }
    }

    private void getSilver()
    {
      var client = new RestClient("https://www.goldapi.io/api/XAG/EUR");
      var request = new RestRequest("",Method.Get);
      request.AddHeader("x-access-token", ConfigurationManager.AppSettings["infoAPIKeyGold"] );
      request.AddHeader("Content-Type", "application/json");
      RestResponse response = client.ExecuteGetAsync(request).Result;
      JObject jsonResponse = JObject.Parse(response.Content);
      try
      {
        float temp = jsonResponse.GetValue("price").ToObject<float>();
        if (this.silverPriceInEuro < temp)
        {
          this.silverPriceInEuroTrend = TrendUp;
        }
        else
        {
          if (this.silverPriceInEuro > temp)
          {
            this.silverPriceInEuroTrend = TrendDown;
          }
          else
          {
            this.silverPriceInEuroTrend = TrendEqual;
          }
        }
        this.silverPriceInEuro = temp;
      }
      catch (NullReferenceException nre)
      {
        logger.Error(nre, "Null beim Abfragen des Silberpreises");
      }
    }

    private void getFuelprice()
    {
      string url = "https://creativecommons.tankerkoenig.de/json/prices.php?ids="+ ConfigurationManager.AppSettings["infoAPIIDFuel"] + "&apikey="+ ConfigurationManager.AppSettings["infoAPIKeyFuel"];
      var client = new RestClient(url);
      var request = new RestRequest("",Method.Get);
      request.AddHeader("Content-Type", "application/json");
      RestResponse response = client.ExecuteGetAsync(request).Result;
      JObject jsonResponse = JObject.Parse(response.Content);
      try
      {
        float temp = jsonResponse.GetValue("prices").First.First["e5"].ToObject<float>();
        if (this.unleadedFuelPriceInEuro < temp)
        {
          this.unleadedFuelPriceInEuroTrend = TrendUp;
        }
        else
        {
          if (this.unleadedFuelPriceInEuro > temp)
          {
            this.unleadedFuelPriceInEuroTrend = TrendDown;
          }
          else
          {
            this.unleadedFuelPriceInEuroTrend = TrendEqual;
          }
        }
        this.unleadedFuelPriceInEuro = temp;
        temp = jsonResponse.GetValue("prices").First.First["e10"].ToObject<float>();
        if (this.unleadedBioFuelPriceInEuro < temp)
        {
          this.unleadedBioFuelPriceInEuroTrend = TrendUp;
        }
        else
        {
          if (this.unleadedBioFuelPriceInEuro > temp)
          {
            this.unleadedBioFuelPriceInEuroTrend = TrendDown;
          }
          else
          {
            this.unleadedBioFuelPriceInEuroTrend = TrendEqual;
          }
        }
        this.unleadedBioFuelPriceInEuro = temp;
        temp = jsonResponse.GetValue("prices").First.First["diesel"].ToObject<float>();
        if (this.dieselFuelPriceInEuro < temp)
        {
          this.dieselFuelPriceInEuroTrend = TrendUp;
        }
        else
        {
          if (this.dieselFuelPriceInEuro > temp)
          {
            this.dieselFuelPriceInEuroTrend = TrendDown;
          }
          else
          {
            this.dieselFuelPriceInEuroTrend = TrendEqual;
          }
        }
        this.dieselFuelPriceInEuro = temp;
      }
      catch (NullReferenceException nre)
      {
        logger.Error(nre, "Null beim Abfragen des Treibstoffpreises");
      }
    }

    private void getBitcoin()
    {
      var client = new RestClient("https://rest.coinapi.io/v1/exchangerate/BTC/EUR");
      var request = new RestRequest("",Method.Get);
      request.AddHeader("Content-Type", "application/json");
      request.AddHeader("X-CoinAPI-Key", ConfigurationManager.AppSettings["infoAPIKeyBitcoin"]);
      RestResponse response = client.ExecuteGetAsync(request).Result;
      JObject jsonResponse = JObject.Parse(response.Content);
      try
      {
        float temp = jsonResponse.GetValue("rate").ToObject<float>();
        if (this.bitcoinInEuro < temp)
        {
          this.bitcoinInEuroTrend = TrendUp;
        }
        else
        {
          if (this.bitcoinInEuro > temp)
          {
            this.bitcoinInEuroTrend = TrendDown;
          }
          else
          {
            this.bitcoinInEuroTrend = TrendEqual;
          }
        }
        this.bitcoinInEuro = temp;
      }
      catch (NullReferenceException nre)
      {
        logger.Error(nre, "Null beim Abfragen des Bitcoinpreises");
      }
    }

  }
}
