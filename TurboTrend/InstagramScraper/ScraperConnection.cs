﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Data;
using TurboTrend.Model;
using TurboTrend.Business;

namespace TurboTrend.InstagramScraper
{
    public class ScraperConnection
    {
        public Account[] accList;

        public ScraperConnection() { } // Do nothing

        public DataTable interpretHashTagAndSearch(string sUserInput)
        {
            List<string> uniqueSearch = new List<string>();

            // Remove the # incase the user includes them
            while (sUserInput.Contains('#'))
            {
                sUserInput.Remove(sUserInput.IndexOf("#"), 1);
            }

            // If commas and a spaces are used to seperate the hashtags, split the search by that term
            if (sUserInput.Contains(", "))
            {
                List<string> containsCommaSpace = checkInput(", ", sUserInput);
                foreach (string element in containsCommaSpace) { if (!uniqueSearch.Contains(element)) { uniqueSearch.Add(element); } }
            }
            // If commas are used to seperate the hashtags, split the search by that term
            else if (sUserInput.Contains(","))
            {
                List<string> containsComma = checkInput(",", sUserInput);
                foreach (string element in containsComma) { if (!uniqueSearch.Contains(element)) { uniqueSearch.Add(element); } }
            }
            // If spaces are used to seperate the hashtags, split the search by that term
            else if (sUserInput.Contains(' '))
            {
                List<string> containsSpace = checkInput(" ", sUserInput);
                foreach (string element in containsSpace) { if (!uniqueSearch.Contains(element)) { uniqueSearch.Add(element); } }
            }
            else
            {
                uniqueSearch.Add(sUserInput);
            }

            DatabaseConnection db = new DatabaseConnection();


            // Adds the searched hashtag, and the business that searched them into the database
            foreach (string term in uniqueSearch)
            {
                db.InsertHashtagIntoDB(term);
            }

            if (uniqueSearch.Count > (new ProjectConfig().MaxSearchTerms))
            {
                List<string> maxSearchLimitHit = new List<string>();
                for (int i = 0; i < (new ProjectConfig().MaxSearchTerms); i++)
                {
                    maxSearchLimitHit.Add(uniqueSearch[i]);
                }

                getInfoFromHashtag(maxSearchLimitHit.ToArray());
            }
            else
            {
                getInfoFromHashtag(uniqueSearch.ToArray());
            }


            
            DataTable dbTable = db.InsertInfluencerIntoDB(accList);


            return dbTable;
        }

        private List<string> checkInput(string sCheck, string sInput)
        {
            List<string> sReturnArray = new List<string>();

            if (sInput.Contains(sCheck))
            {
                string[] containsSCheck = sInput.Split(sCheck.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string element in containsSCheck)
                {
                    sReturnArray.Add(element);
                }
            }

            return sReturnArray;
        }

        private void getInfoFromHashtag(string[] sHashtags)
        {
            try
            {
                // Formats the hashtag array to the correct format for the python script
                // The correct format is "hashtag,hashtag,hashtag"

                string sHashes = "";
                for (int i = 0; i < sHashtags.Length; i++)
                {
                    if (i < sHashtags.Length - 1)
                    {
                        sHashes += sHashtags[i] + ",";
                    }
                    else
                    {
                        sHashes += sHashtags[i];
                    }
                }


                ProjectConfig settings = new ProjectConfig();

                Process proc = new Process();
                proc.StartInfo.FileName = settings.PythonLocation;
                proc.StartInfo.Arguments = string.Format("{0} {1}", settings.ScraperLocation, sHashes);
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.UseShellExecute = false;


                proc.Start();

                StreamReader sReader = proc.StandardOutput;
                StreamReader sReaderError = proc.StandardError;


                string errorOut = sReaderError.ReadToEnd();
                string output = sReader.ReadToEnd();

                proc.WaitForExit();

                if (output.Contains("THEREISANERROR-.-") || output.Contains("No command arguments found."))
                { // This error is generated by the Python script when no accounts or arguments are found
                    accList = new Account[0];
                }
                else
                {
                    interpretOutput(output);
                }
            }
            catch(Exception error)
            {
                // If we hit an error, just assume that there are no accounts searchable.
                accList = new Account[0];
            }
        }

        private void interpretOutput(string sInput)
        {// Takes the big string outputted by the python script, breaks it down and stores it.
            List<string> tempList = (sInput.Split(@"\".ToCharArray())).ToList();

            // Just checking the list doesn't contain anything it's not suppose to.
            while (tempList.Contains(@"\"))
            {
                tempList.Remove(@"\");
            }
            while (tempList.Contains(""))
            {
                tempList.Remove("");
            }

            List<Account> accHolder = new List<Account>();
            string sAccountNamesAdded = "";
            for (int i = 0; i < tempList.Count; i += 4)
            {
                // The information is outputted by Python in the following format:
                // /AccountURL/Followers/Following/Comments - We use this information to extract our data.
                string compareName = tempList[i].Substring("https://www.instagram.com/".Length);

                if (!sAccountNamesAdded.Contains(compareName))
                {
                    Account acTemp = new Account();
                    acTemp.accountUrl = tempList[i];
                    acTemp.accountName = compareName;
                    acTemp.accountFollowers = numConverter(tempList[i + 1]);
                    acTemp.accountFollowing = numConverter(tempList[i + 2]);
                    acTemp.accountPosts = numConverter(tempList[i + 3]);
                    accHolder.Add(acTemp);

                    sAccountNamesAdded += compareName + ",";
                }
            }

            accList = accHolder.ToArray();
        }

        private string numConverter(string inputString)
        {
            if (!inputString.Contains("k") & !inputString.Contains("m"))
            {
                return inputString.Replace(",", "");
            }

            //Replaces commas with a ., as the number is converted to a double and multiplied from there.
            inputString = inputString.Replace(",", ".");

            if (inputString.Contains('k'))
            {
                inputString = (Convert.ToDouble(inputString.Substring(0, inputString.IndexOf("k"))) * 1000).ToString();
            }

            //If the number is abbreviated with a m, firstly remove the m with Substring, then multiple by 1000000 and convert back to a string. 
            else if (inputString.Contains('m'))
            {
                inputString = (Convert.ToDouble(inputString.Substring(0, inputString.IndexOf("m"))) * 1000000).ToString();
            }

            return inputString;
        }
    }
}