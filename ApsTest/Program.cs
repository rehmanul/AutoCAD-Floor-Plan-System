using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.SDKManager;
using Autodesk.Authentication;

namespace ApsTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var sdkManager = new SDKManager(new ApsConfiguration("YOUR_CLIENT_ID", "YOUR_CLIENT_SECRET"));
            var auth = new AuthenticationClient(sdkManager);
            var token = await auth.GetTwoLeggedTokenAsync(new List<Scope> { Scope.CodeAll });
            Console.WriteLine(token.AccessToken);
        }
    }
}
