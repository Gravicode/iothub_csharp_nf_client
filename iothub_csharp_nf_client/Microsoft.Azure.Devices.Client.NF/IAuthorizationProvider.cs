// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#define NANOFRAMEWORK

#if !NETMF && !NANOFRAMEWORK
using System.Threading.Tasks;
#endif

namespace Microsoft.Azure.Devices.Client
{
    interface IAuthorizationProvider
    {
#if !NETMF && !NANOFRAMEWORK
        Task<string> GetPasswordAsync();
#else
        string GetPassword();
#endif
    }
}
