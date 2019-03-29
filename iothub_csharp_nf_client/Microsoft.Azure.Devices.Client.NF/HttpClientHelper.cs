// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Client
{
    using System;
    using System.IO;
    using System.Net;
    using System.Collections;
    using System.Text;
    using Microsoft.Azure.Devices.Shared;

    using Microsoft.Azure.Devices.Client.Extensions;
    using System.Security.Cryptography.X509Certificates;
    using System.Net.Sockets;
    using System.Net.Security;

    sealed class HttpClientHelper : IDisposable
    {
        readonly Uri baseAddress;
        readonly IAuthorizationProvider authenticationHeaderProvider;
        bool isDisposed;
        
        public HttpClientHelper(
            Uri baseAddress,
            IAuthorizationProvider authenticationHeaderProvider,
            TimeSpan timeout)
        {
            this.baseAddress = baseAddress;
            this.authenticationHeaderProvider = authenticationHeaderProvider;
        }

        public HttpWebResponse Get(
            string requestUri,
            Hashtable customHeaders)
        {
            return this.Get(requestUri, customHeaders, true);
        }

        public HttpWebResponse Get(
            string requestUri,
            Hashtable customHeaders,
            bool throwIfNotFound)
        {
            try
            {
                using (var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(this.baseAddress.OriginalString + requestUri)))
                {
                    webRequest.Method = "GET";

                    // add authorization header
                    webRequest.Headers.Add("Authorization", this.authenticationHeaderProvider.GetPassword());

                    // add custom headers
                    AddCustomHeaders(webRequest, customHeaders);

                    // perform request and get response
                    // don't close the WebResponse here because we may need to read the response stream later 
                    var webResponse = webRequest.GetResponse() as HttpWebResponse;

                    // message received
                    return webResponse;
                }
            }
            catch (Exception)
            {
                if (throwIfNotFound)
                {
                    throw;
                }
                else
                {
                    return null;
                }
            }
        }

        static void AddCustomHeaders(HttpWebRequest requestMessage, Hashtable customHeaders)
        {
            foreach (var header in customHeaders.Keys)
            {
                requestMessage.Headers.Add(header as string, customHeaders[header] as string);
            }
        }

        static void InsertEtag(HttpWebRequest requestMessage, IETagHolder entity, PutOperationType operationType)
        {
            if (operationType == PutOperationType.CreateEntity)
            {
                return;
            }

            if (operationType == PutOperationType.ForceUpdateEntity)
            {
                const string etag = "\"*\"";
                requestMessage.Headers.Add("IfMatch", etag);
            }
            else
            {
                InsertEtag(requestMessage, entity);
            }
        }

        static void InsertEtag(HttpWebRequest requestMessage, IETagHolder entity)
        {
            if (entity.ETag.IsNullOrWhiteSpace())
            {
                throw new ArgumentException("The entity does not have its ETag set.");
            }

            string etag = entity.ETag;

            if (etag.IndexOf("\"") != 0)
            {
                etag = "\"" + etag;
            }

            if (etag.LastIndexOf("\"") != etag.Length)
            {
                etag = etag + "\"";
            }

            requestMessage.Headers.Add("IfMatch", etag);
        }

        public void Post(
            string requestUri,
            object entity,
            Hashtable customHeaders)
        {
            X509Certificate letsEncryptCACert = new X509Certificate(letsEncryptCACertificate);

            // get host entry for test site
            //IPHostEntry hostEntry = Dns.GetHostEntry(this.baseAddress.Host);
            IPAddress ipAddress = IPAddress.Parse("13.76.217.46");
            IPEndPoint ep = new IPEndPoint(ipAddress, 443);
            // need an IPEndPoint from that one above
            //IPEndPoint ep = new IPEndPoint(hostEntry.AddressList[0], 443);
            using (Socket mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    Console.WriteLine("Connecting...");

                    // connect socket
                    mySocket.Connect(ep);

                    Console.WriteLine("Authenticating with server...");

                    // setup SSL stream
                    SslStream ss = new SslStream(mySocket);

                    ///////////////////////////////////////////////////////////////////////////////////
                    // Authenticating the server can be handled in one of three ways:
                    //
                    // 1. By providing the root CA certificate of the server being connected to.
                    // 
                    // 2. Having the target device preloaded with the root CA certificate.
                    // 
                    // !! NOT SECURED !! NOT RECOMENDED !!
                    // 3. Forcing the authentication workflow to NOT validate the server certificate.
                    //
                    /////////////////////////////////////////////////////////////////////////////////// 

                    // option 1 
                    // setup authentication (add CA root certificate to the call)
                    ss.AuthenticateAsClient(this.baseAddress.Host, null, letsEncryptCACert, SslProtocols.TLSv11);

                    using (var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(this.baseAddress.OriginalString + requestUri)))
                    {
                        //webRequest.ProtocolVersion = HttpVersion.Version11;
                        webRequest.KeepAlive = true;
                        webRequest.Method = "POST";

                        // add authorization header
                        webRequest.Headers.Add("Authorization", this.authenticationHeaderProvider.GetPassword());

                        // add custom headers
                        AddCustomHeaders(webRequest, customHeaders);
                        webRequest.AllowWriteStreamBuffering = true;

                        if (entity == null)
                        {
                            webRequest.ContentLength = 0;
                        }
                        else
                        {
                            if (entity.GetType().Equals(typeof(MemoryStream)))
                            {
                                // need to set these before getting the request stream
                                webRequest.ContentLength = ((Stream)entity).Length;

                                int totalBytes = 0;
                                using (var requestStream = webRequest.GetRequestStream())
                                {
                                    var buffer = new byte[256];
                                    var bytesRead = 0;

                                    while ((bytesRead = ((Stream)entity).Read(buffer, 0, 256)) > 0)
                                    {
                                        requestStream.Write(buffer, 0, bytesRead);

                                        totalBytes += bytesRead;
                                    }
                                }

                            }
                            else if (entity.GetType().Equals(typeof(string)))
                            {

                                var buffer = Encoding.UTF8.GetBytes(entity as string);

                                // need to set these before getting the request stream
                                webRequest.ContentLength = buffer.Length;
                                webRequest.ContentType = CommonConstants.BatchedMessageContentType;

                                int bytesSent = 0;

                                using (var requestStream = webRequest.GetRequestStream())
                                {
                                    var chunkBytes = 0;

                                    while (bytesSent < buffer.Length)
                                    {
                                        // calculate bytes count for this chunk
                                        chunkBytes = (buffer.Length - bytesSent) < 256 ? (buffer.Length - bytesSent) : 256;

                                        // write chunk
                                        requestStream.Write(buffer, bytesSent, chunkBytes);

                                        // update counter
                                        bytesSent += chunkBytes;
                                    }
                                }

                            }
                            else
                            {
                                webRequest.ContentLength = 0;
                            }
                        }

                        // perform request and get response
                        using (var webResponse = webRequest.GetResponse() as HttpWebResponse)
                        {
                            if (webResponse.StatusCode == HttpStatusCode.NoContent)
                            {
                                // success!
                                return;
                            }
                            else
                            {
                                throw new WebException("", null, WebExceptionStatus.ReceiveFailure, webResponse);
                            }
                        }
                    }
                }
                catch(Exception ex) {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public void Delete(
            string requestUri,
            IETagHolder etag,
            Hashtable customHeaders)
        {
            using (var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(this.baseAddress.OriginalString + requestUri)))
            {
                webRequest.Method = "DELETE";

                // add authorization header
                webRequest.Headers.Add("Authorization", this.authenticationHeaderProvider.GetPassword());

                // add custom headers
                AddCustomHeaders(webRequest, customHeaders);

                // add ETag header
                InsertEtag(webRequest, etag);

                // perform request and get response
                using (var webResponse = webRequest.GetResponse() as HttpWebResponse)
                {
                    if (webResponse.StatusCode != HttpStatusCode.NoContent)
                    {
                        throw new WebException("", null, WebExceptionStatus.ReceiveFailure, webResponse);
                    }
                    // else it's a success and there's nothing to do but exit the method.
                }
            }
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
            }
        }
        // X509 RSA key PEM format 2048 bytes
        private const string letsEncryptCACertificate =
@"-----BEGIN CERTIFICATE-----
MIIFjTCCA3WgAwIBAgIRANOxciY0IzLc9AUoUSrsnGowDQYJKoZIhvcNAQELBQAw
TzELMAkGA1UEBhMCVVMxKTAnBgNVBAoTIEludGVybmV0IFNlY3VyaXR5IFJlc2Vh
cmNoIEdyb3VwMRUwEwYDVQQDEwxJU1JHIFJvb3QgWDEwHhcNMTYxMDA2MTU0MzU1
WhcNMjExMDA2MTU0MzU1WjBKMQswCQYDVQQGEwJVUzEWMBQGA1UEChMNTGV0J3Mg
RW5jcnlwdDEjMCEGA1UEAxMaTGV0J3MgRW5jcnlwdCBBdXRob3JpdHkgWDMwggEi
MA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCc0wzwWuUuR7dyXTeDs2hjMOrX
NSYZJeG9vjXxcJIvt7hLQQWrqZ41CFjssSrEaIcLo+N15Obzp2JxunmBYB/XkZqf
89B4Z3HIaQ6Vkc/+5pnpYDxIzH7KTXcSJJ1HG1rrueweNwAcnKx7pwXqzkrrvUHl
Npi5y/1tPJZo3yMqQpAMhnRnyH+lmrhSYRQTP2XpgofL2/oOVvaGifOFP5eGr7Dc
Gu9rDZUWfcQroGWymQQ2dYBrrErzG5BJeC+ilk8qICUpBMZ0wNAxzY8xOJUWuqgz
uEPxsR/DMH+ieTETPS02+OP88jNquTkxxa/EjQ0dZBYzqvqEKbbUC8DYfcOTAgMB
AAGjggFnMIIBYzAOBgNVHQ8BAf8EBAMCAYYwEgYDVR0TAQH/BAgwBgEB/wIBADBU
BgNVHSAETTBLMAgGBmeBDAECATA/BgsrBgEEAYLfEwEBATAwMC4GCCsGAQUFBwIB
FiJodHRwOi8vY3BzLnJvb3QteDEubGV0c2VuY3J5cHQub3JnMB0GA1UdDgQWBBSo
SmpjBH3duubRObemRWXv86jsoTAzBgNVHR8ELDAqMCigJqAkhiJodHRwOi8vY3Js
LnJvb3QteDEubGV0c2VuY3J5cHQub3JnMHIGCCsGAQUFBwEBBGYwZDAwBggrBgEF
BQcwAYYkaHR0cDovL29jc3Aucm9vdC14MS5sZXRzZW5jcnlwdC5vcmcvMDAGCCsG
AQUFBzAChiRodHRwOi8vY2VydC5yb290LXgxLmxldHNlbmNyeXB0Lm9yZy8wHwYD
VR0jBBgwFoAUebRZ5nu25eQBc4AIiMgaWPbpm24wDQYJKoZIhvcNAQELBQADggIB
ABnPdSA0LTqmRf/Q1eaM2jLonG4bQdEnqOJQ8nCqxOeTRrToEKtwT++36gTSlBGx
A/5dut82jJQ2jxN8RI8L9QFXrWi4xXnA2EqA10yjHiR6H9cj6MFiOnb5In1eWsRM
UM2v3e9tNsCAgBukPHAg1lQh07rvFKm/Bz9BCjaxorALINUfZ9DD64j2igLIxle2
DPxW8dI/F2loHMjXZjqG8RkqZUdoxtID5+90FgsGIfkMpqgRS05f4zPbCEHqCXl1
eO5HyELTgcVlLXXQDgAWnRzut1hFJeczY1tjQQno6f6s+nMydLN26WuU4s3UYvOu
OsUxRlJu7TSRHqDC3lSE5XggVkzdaPkuKGQbGpny+01/47hfXXNB7HntWNZ6N2Vw
p7G6OfY+YQrZwIaQmhrIqJZuigsrbe3W+gdn5ykE9+Ky0VgVUsfxo52mwFYs1JKY
2PGDuWx8M6DlS6qQkvHaRUo0FMd8TsSlbF0/v965qGFKhSDeQoMpYnwcmQilRh/0
ayLThlHLN81gSkJjVrPI0Y8xCVPB4twb1PFUd2fPM3sA1tJ83sZ5v8vgFv2yofKR
PB0t6JzUA81mSqM3kxl5e+IZwhYAyO0OTg3/fs8HqGTNKd9BqoUwSRBzp06JMg5b
rUCGwbCUDI0mxadJ3Bz4WxR6fyNpBK2yAinWEsikxqEt
-----END CERTIFICATE-----";
    }
}