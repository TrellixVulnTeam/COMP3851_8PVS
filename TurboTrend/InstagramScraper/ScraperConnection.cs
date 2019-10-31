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
                // The correct format is "hashtag, hashtag, hashtag"

                string sHashes = "";
                for (int i = 0; i < sHashtags.Length; i++)
                {
                    if (i < sHashtags.Length - 1)
                    {
                        sHashes += sHashtags[i] + ", ";
                    }
                    else
                    {
                        sHashes += sHashtags[i];
                    }
                }

                ProjectConfig settings = new ProjectConfig();

                //Making a process to install the libraries before trying to run the .py file.
                Process batStart = new Process();
                batStart.StartInfo = new ProcessStartInfo(settings.libBatLocation);
                batStart.StartInfo.CreateNoWindow = true;
                batStart.Start();
                batStart.WaitForExit();

                string output;
                string stderr;

                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = settings.PythonLocation;
                start.Arguments = string.Format("\"{0}\" \"{1}\"", settings.ScraperLocation, sHashes);
                start.UseShellExecute = false;// Do not use OS shell
                start.CreateNoWindow = true; // We don't need new window
                start.RedirectStandardOutput = true;// Any output, generated by application will be redirected back
                start.RedirectStandardError = true; // Any error in standard output will be redirected back (for example exceptions)
                using (Process process = Process.Start(start))
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        stderr = process.StandardError.ReadToEnd(); // Here are the exceptions from our Python script
                        output = reader.ReadToEnd(); // Here is the result of StdOut(for example: print "test")
                    }
                }

                if (output.Contains("THEREISANERROR-.-") || output.Contains("No command arguments found."))
                { // This error is generated by the Python script when no accounts or arguments are found
                    accList = new Account[0];
                }
                else
                {
                    interpretOutput(output);
                }
            }
            catch
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
            for (int i = 0; i < tempList.Count; i += 4)
            {
                // The information is outputted by Python in the following format:
                // /AccountURL/Followers/Following/Comments - We use this information to extract our data.

                Account acTemp = new Account();
                acTemp.accountUrl = tempList[i];
                acTemp.accountName = tempList[i].Substring("https://www.instagram.com/".Length);
                acTemp.accountFollowers = numConverter(tempList[i + 1]);
                acTemp.accountFollowing = numConverter(tempList[i + 2]);
                acTemp.accountPosts = numConverter(tempList[i + 3]);

                accHolder.Add(acTemp);
            }

            accList = accHolder.ToArray();
        }

        private string numConverter (string s)
        {
            //Removing the letter at the end of the larger abbreviated numbers.
            //Isolating the charater to a variable to be checked later.
            string c = s.Substring(s.Length - 1, 1);
            //Removing both commas and fullstops from within the string.
            s = s.Replace(",", "");
            s = s.Replace(".", "");
            //If the number is abbreviated with a k, firstly remove the k with Substring, then multiple by 1000 and convert back to a string. 
            if (c == "k")
            {
                s = (Convert.ToInt32(s.Substring(0, s.Length - 1)) * 1000).ToString();
            }
            //If the number is abbreviated with a m, firstly remove the m with Substring, then multiple by 1000000 and convert back to a string. 
            else if (c == "m")
            {
                s = (Convert.ToInt32(s.Substring(0, s.Length - 1)) * 1000000).ToString();
            }

            return s;
        }
    }
}