﻿/****************************************************************************
Copyright (c) 2013-2015 scutgame.com

http://www.scutgame.com

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
****************************************************************************/
using System;
using System.Net.Sockets;
using System.ServiceModel;
using System.Web;
using ZyGames.Framework.Common;
using ZyGames.Framework.Common.Configuration;
using ZyGames.Framework.Common.Log;
using ZyGames.Framework.Game.Service;
using ZyGames.Framework.RPC.IO;
using ZyGames.Framework.RPC.Wcf;

namespace ZyGames.Framework.Game.Contract
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpServiceRequest
    {
        private static bool EnableError;
        private static string ErrorNotFind;
        private static string ErrorConnected;
        private static string ErrorTimeout;
        private static string ErrorUnknown;

        static HttpServiceRequest()
        {
            EnableError = ConfigUtils.GetSetting("Enable.Error").ToBool();
            ErrorNotFind = ConfigUtils.GetSetting("Error.NotFind");
            ErrorConnected = ConfigUtils.GetSetting("Error.Connected");
            ErrorTimeout = ConfigUtils.GetSetting("Error.Timeout");
            ErrorUnknown = ConfigUtils.GetSetting("Error.Unknown");

        }
        private HttpGet httpGet;
        private HttpGameResponse response;
        private MessageStructure _buffer;
        private string ParamString;
        private string SessionId;
        private int GameId;
        private int ServerId;
        private int ActionId;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        public HttpServiceRequest(HttpContext context)
        {
            _buffer = new MessageStructure();
            httpGet = new HttpGet(context.Request);
            response = new HttpGameResponse(context.Response);
            ParamString = httpGet.ParamString;
            SessionId = httpGet.SessionId;
        }
        /// <summary>
        /// 
        /// </summary>
        public void Request()
        {
            ReadParam();
            RequestSettings settings = new RequestSettings(GameId, ServerId, "", ParamString);

            byte[] sendBuffer = new byte[0];
            RequestError error = RequestError.Success;
            try
            {
                ServiceRequest.Request(settings, out sendBuffer);
            }
            catch (CommunicationObjectFaultedException fault)
            {
                TraceLog.WriteError("The wcfclient request faulted:{0}", fault);
                error = RequestError.Closed;
                ServiceRequest.ResetChannel(settings);
            }
            catch (Exception ex)
            {
                if (ex.InnerException is SocketException)
                {
                    var sex = ex.InnerException as SocketException;
                    TraceLog.WriteError("The wcfclient request connect:{0}-{1}", sex.SocketErrorCode, sex);
                    if (sex.SocketErrorCode == SocketError.TimedOut)
                    {
                        error = RequestError.Timeout;
                    }
                    else
                    {
                        error = RequestError.UnableConnect;
                    }
                }
                else
                {
                    TraceLog.WriteError("The wcfclient request error:{0}", ex);
                    error = RequestError.Unknown;
                }
                ServiceRequest.ResetChannel(settings);
            }
            switch (error)
            {
                case RequestError.Success:
                    WriteBuffer(sendBuffer);
                    break;
                case RequestError.Closed:
                case RequestError.NotFindService:
                    WriteNotFindError();
                    break;
                case RequestError.UnableConnect:
                    WriteConnectionError();
                    break;
                case RequestError.Timeout:
                    WriteTimeoutError();
                    break;
                case RequestError.Unknown:
                    WriteUnknownError();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("RequestError", error, "Not process RequestError enum.");
            }
        }

        protected void ReadParam()
        {
            if (!CheckGameServer())
            {
                GameId = httpGet.GetInt("gametype");
                ServerId = httpGet.GetInt("serverid");
                ServerId = ServerId > 0 ? ServerId : 1;
            }
            ActionId = httpGet.GetInt("actionId");
        }


        private bool CheckGameServer()
        {
            if (httpGet.Contains("sid"))
            {
                string[] array = httpGet.GetString("sid").Split('|');
                if (array.Length > 2)
                {
                    string sid = array[0];
                    GameId = array[1].ToInt();
                    ServerId = array[2].ToInt();
                    return true;
                }
            }
            return false;
        }

        protected void WriteBuffer(byte[] buffer)
        {
            response.BinaryWrite(buffer);
        }

        protected void WriteNotFindError()
        {
            DoWriteError(ErrorNotFind);
            TraceLog.WriteError("Unable to find connection gameid:{0} serverId:{1} error.", GameId, ServerId);
        }

        protected void WriteConnectionError()
        {
            DoWriteError(ErrorConnected);
            TraceLog.WriteError("The connection to gameid:{0} serverId:{1} error.", GameId, ServerId);
        }

        protected void WriteTimeoutError()
        {
            DoWriteError(ErrorTimeout);
            TraceLog.WriteError("The socket-server [gameid:{0} serverId:{1} timeout error.", GameId, ServerId);
        }

        protected void WriteUnknownError()
        {
            DoWriteError(ErrorUnknown);
            TraceLog.WriteError("The receive to gameid:{0} serverId:{1} error:{2}", GameId, ServerId);
        }

        private void DoWriteError(string errorInfo)
        {
            var head = new MessageHead
            {
                ErrorCode = (int)MessageError.SystemError,
                Action = ActionId
            };
            if (EnableError)
            {
                head.ErrorInfo = errorInfo;
            }
            _buffer.WriteBuffer(head);
            WriteBuffer(_buffer.ReadBuffer());
        }


    }
}