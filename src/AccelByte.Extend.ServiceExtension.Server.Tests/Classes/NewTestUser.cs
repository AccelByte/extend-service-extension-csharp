// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;

using AccelByte.Sdk.Core;
using AccelByte.Sdk.Core.Repository;
using AccelByte.Sdk.Core.Util;
using AccelByte.Sdk.Api;
using AccelByte.Sdk.Api.Iam.Model;

namespace AccelByte.Extend.ServiceExtension.Server.Tests
{
    public class NewTestUser
    {
        private string _UserName;

        private string _UserPassword;

        private readonly bool _DeleteOnLogout;

        private readonly DefaultTokenRepository _TokenRepo;

        private readonly AccelByteSDK _AdminSdkClient;

        public AccelByteSDK SdkObject { get; }

        public string AccessToken
        {
            get => _TokenRepo.Token;
        }

        public string UserId { get; }

        public NewTestUser(AccelByteSDK adminSdkClient, bool deleteOnLogout)
        {
            _DeleteOnLogout = deleteOnLogout;
            _AdminSdkClient = adminSdkClient;
            string rStr = Helper.GenerateRandomId(8);

            _UserName = $"csharpsdk_{rStr}";
            _UserPassword = Helper.GenerateRandomPassword(10);
            string user_email = $"{_UserName}@dummy.com";

            AccountCreateUserRequestV4 newUser = new AccountCreateUserRequestV4()
            {
                AuthType = "EMAILPASSWD",
                EmailAddress = user_email,
                Password = _UserPassword,
                DisplayName = $"AB Extend Demo Test User {rStr}",
                Username = _UserName,
                Country = "US",
                DateOfBirth = "1995-01-10"
            };

            AccountCreateUserResponseV4? cuResp = _AdminSdkClient.Iam.UsersV4.PublicCreateUserV4Op
                .Execute(newUser, _AdminSdkClient.Namespace);
            if (cuResp != null)
                UserId = cuResp.UserId!;
            else
                throw new Exception("Could not create a test user.");

            _TokenRepo = new DefaultTokenRepository();
            SdkObject = AccelByteSDK.Builder
                .UseDefaultConfigRepository()
                .UseDefaultHttpClient()
                .SetTokenRepository(_TokenRepo)
                .Build();
        }

        public void Login()
        {
            SdkObject.LoginUser(_UserName, _UserPassword);
        }

        public void Logout()
        {
            SdkObject.Logout();
            if (_DeleteOnLogout)
            {
                _AdminSdkClient.Iam.Users.AdminDeleteUserInformationV3Op
                    .Execute(_AdminSdkClient.Namespace, UserId);
            }
        }
    }
}
