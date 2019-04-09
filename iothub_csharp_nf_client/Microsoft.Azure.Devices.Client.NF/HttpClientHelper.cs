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
                    if (DeviceClient.CACert != null) webRequest.HttpsAuthentCert = DeviceClient.CACert;
                    webRequest.SslProtocols = System.Net.Security.SslProtocols.TLSv12;
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


            // get host entry for test site
            //IPHostEntry hostEntry = Dns.GetHostEntry(this.baseAddress.Host);
            //IPAddress ipAddress = IPAddress.Parse("13.76.217.46");
            //IPEndPoint ep = new IPEndPoint(ipAddress, 443);
            // need an IPEndPoint from that one above
            //IPEndPoint ep = new IPEndPoint(hostEntry.AddressList[0], 443);
            //using (Socket mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))

            try
            {
                Console.WriteLine("Connecting...");

                // connect socket
                //mySocket.Connect(ep);

                Console.WriteLine("Authenticating with server...");

                // setup SSL stream
                //SslStream ss = new SslStream(mySocket);

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
                //ss.AuthenticateAsClient(this.baseAddress.Host, null, letsEncryptCACert, SslProtocols.TLSv11);
                var FullUrl = this.baseAddress.OriginalString + requestUri;
                using (var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(FullUrl)))
                {

                    if (DeviceClient.CACert != null) webRequest.HttpsAuthentCert = DeviceClient.CACert;
                    webRequest.SslProtocols = System.Net.Security.SslProtocols.TLSv12;
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            //}
        }

        public void Delete(
            string requestUri,
            IETagHolder etag,
            Hashtable customHeaders)
        {
            using (var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(this.baseAddress.OriginalString + requestUri)))
            {
                if (DeviceClient.CACert != null) webRequest.HttpsAuthentCert = DeviceClient.CACert;
                webRequest.SslProtocols = System.Net.Security.SslProtocols.TLSv12;
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

    }
}