#  Chapter 9: Running and Testing the Service

In this chapter, we will go over how to run your Guild Service and perform some basic tests to 
ensure that everything is working as expected.

# 9.1 Running the Service

## Setup

To be able to run this sample app, you will need to follow these setup steps.

- Create a docker compose `.env` file by copying the content of [.env.template](../.env.template) file.

- Fill in the required environment variables in `.env` file as shown below.

   ```txt
   AB_BASE_URL='http://test.accelbyte.io'     # Your environment's domain Base URL
   AB_CLIENT_ID='xxxxxxxxxx'                  # Use Client ID from the Setup section
   AB_CLIENT_SECRET='xxxxxxxxxx'              # Use Client Secret from the Setup section
   AB_NAMESPACE='xxxxxxxxxx'                  # Use Namespace ID from the Setup section
   PLUGIN_GRPC_SERVER_AUTH_ENABLED=true       # Enable or disable access token and permission verification
   BASE_PATH='/guild'                         # The base path used for the app
   ```

  > :info: **PLUGIN_GRPC_SERVER_AUTH_ENABLED**: If 'false' will bypass the validation being set on the endpoint `permission.action` and `permission.resource` [creating-new-endpoint](6-creating-new-endpoint.md#6-creating-a-new-endpoint)

- Ensure you have configured all required permission for your clientId, in this custom service we're using:
  > :exclamation: For AGS Starter customers, you don't need to add the permissions. All confidential IAM clients already contain the necessary permissions.
  - ADMIN:ROLE [READ]
    - It was needed since we define our permission as `ADMIN:` in the service.proto
  - ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [CREATE,READ,UPDATE,DELETE]
    - It was needed since we access cloudsave game record endpoint which requires the above permission

- (Optional) `grpc-plugin-dependencies` mentioned in [chapter 4](4-installation-and-setup.md) is up and running if you needed the observability stack

## Change API base path (Optional)

To change the base path you just need to define it in the envar `BASE_PATH`


## Building, Running, and Testing Locally

To build this sample app, use the following command.

```
make build
```

For more details about these commands, see [Makefile](../Makefile).

## Running

To run the existing docker image of this sample app which has been built before, use the following command.

```
docker compose up
```

OR

To build, create a docker image, and run this sample app in one go, use the following command.

```
docker compose up --build
```

## Testing

After starting the service, you can test it to make sure it's working correctly.

We will use curl command to test our service. For example, to test `CreateOrUpdateGuildProgress` endpoint, you can run:

Be sure to use replace the `accessToken`, `namespace`. Since the endpoint require admin permission `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD`, ensure your accessToken has the admin permission.

```bash
curl -X 'POST' \
  'http://localhost:8000/guild/v1/admin/namespace/<your-namespace>/progress' \
  -H 'accept: application/json' \
  -H 'Authorization: Bearer <accessToken>' \
  -H 'Content-Type: application/json' \
  -d '{
  "guildProgress": {
    "guildId": "123456789",
    "namespace": "<your-namespace>",
    "objectives": {
      "target1": 0
    }
  }
}'
```

And to test `GetGuildProgress` endpoint:

```bash
curl -X 'GET' \
  'http://localhost:8000/guild/v1/admin/namespace/<your-namespace>/progress/123456789' \
  -H 'accept: application/json' \
  -H 'Authorization: Bearer <accessToken>'
```

You should see the updated guild progress in the response.

Alternatively you can test using the swagger UI, by going to `http://localhost:8000/guild/apidocs/`

![swagger-inteface](images/swagger-interface.png)


