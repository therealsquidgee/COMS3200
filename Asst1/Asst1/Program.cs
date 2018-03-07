using System;
using System.Net.Sockets;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Net;

namespace Asst1
{
    class Program
    {
        static bool prompt = true;
        static string url = "";

        static void Main(string[] args)
        {
            while (true)
            {
                Console.Write("Please enter a URL to send a request to, or type exit to exit application: ");

                //Should we prompt the user?
                if (prompt)
                {
                    url = Console.ReadLine();
                }

                if(url.ToLower() == "exit")
                {
                    break;
                }

                Console.WriteLine("");

                //Get rid of the http (as per asst spec) and split the url into parts.
                var urlParts = url.Replace("https://", "").Replace("http://", "").Split('/');

                //Grab the host and split it up into name and port.
                var hostParts = urlParts[0].Split(':');
                var hostName = urlParts[0];
                int hostPort = hostParts.Length < 2 ? 80 : int.Parse(urlParts[1]);
                int response = 0;

                //Set up the socket and establish a connection.
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(hostName, hostPort);

                // Get a reference to the local and remote endpoints (for printing later).
                var localEndPoint = (IPEndPoint)sock.LocalEndPoint;
                var remoteEndPoint = (IPEndPoint)sock.RemoteEndPoint;

                //Send off the request.
                string request = "GET /" + String.Join("", urlParts.Where(x => x != hostName)) + " HTTP/1.1\r\n" +
                    "Host: " + hostName + "\r\n" +
                    "Content-Length: 0\r\n" +
                    "Accept-Encoding: gzip,deflate\r\n" +
                    "\r\n";
                response = sock.Send(Encoding.UTF8.GetBytes(request));

                //Prepare to receive 1kB or less
                var bytesReceived = new byte[1024];
                var page = "";

                //Receive response and decode it into ASCII letters.
                var bytes = sock.Receive(bytesReceived, bytesReceived.Length, 0);
                page = page + Encoding.ASCII.GetString(bytesReceived, 0, bytes);

                Console.WriteLine("HTTP Protocol Analyzer, Written by Ryan Pousson, s4318132");

                //Split the response into lines.
                var pageSections = page.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                // Get the status bit at the start.
                var responseStatus = pageSections[0];

                // The key value bit is everything other than the top line.
                var keyValues = pageSections.Where(x => x != responseStatus).ToList();
                var movedTo = GetValueFor(keyValues, "Location");

                //Now print out the relevant sections.
                var responseString = String.Format("URL Requested: {0}\r\n" +
                    "IP Address, Port of the Server: {1}, {2}\r\n" +
                    "IP Address # Port of this Client: {3}, {4}\r\n" +
                    "Reply Code: {5}\r\n" +
                    "Reply Code Meaning: {6}\r\n" +
                    "Date: {7}\r\n" + //(please convert times to AEST if they are in GMT)
                    "Last-Modified: {8}\r\n" + //similar format(if appropriate to the response)
                    "Content-Encoding: {9}\r\n" + //(if appropriate to the response, you should advertise at least compress, deflate and gzip accepted)
                    "Moved to: {10}\r\n"//(if appropriate to the response)")
                    , url,
                    remoteEndPoint.Address.ToString(),
                    remoteEndPoint.Port.ToString(),
                    localEndPoint.Address.ToString(),
                    localEndPoint.Port.ToString(),
                    responseStatus.Split(' ')[1],
                    responseStatus.Split(new string[] { responseStatus.Split(' ')[1] }, StringSplitOptions.None)[1].Trim(),
                    ConvertDateTimeString(GetValueFor(keyValues, "Date")),
                    ConvertDateTimeString(GetValueFor(keyValues, "Last-Modified")) ?? "not found.",
                    GetValueFor(keyValues, "Content-Encoding") ?? "not found.",
                    movedTo ?? "not found.");

                Console.Write(responseString);
                sock.Close();

                Console.WriteLine("");

                //If we got sent to a new url, go there straight away.
                //Otherwise prompt on the next loop.
                if (movedTo != null && url != movedTo)
                {
                    prompt = false;
                    url = movedTo;
                }
                else
                {
                    prompt = true;
                }

                movedTo = null;
            }
        }

        /// <summary>
        /// Returns the value for a given key
        /// in a list of keyValue pairs represented as List<String>
        /// 
        /// key:value
        /// </summary>
        /// <param name="keyValues"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        static string GetValueFor(List<string> keyValues, string key)
        {
            var pair = keyValues.FirstOrDefault(x => x.Split(':')[0] == key);

            return pair == null ? null : pair.Replace(pair.Split(':')[0] + ":", "").Trim();
        }

        /// <summary>
        /// Converts a timestring from UTC to AEST.
        /// </summary>
        /// <param name="utcTimeString"></param>
        /// <returns></returns>
        static string ConvertDateTimeString(string utcTimeString)
        {
            if(utcTimeString == null)
            {
                return null;
            }

            var formatString = "ddd, dd MMM yyyy HH:mm:ss";
            var dt = DateTime.ParseExact(utcTimeString.Replace(" GMT", ""), formatString, CultureInfo.CurrentCulture);

            return dt.ToLocalTime().ToString(formatString) + " AEST";
        }
    }
}
