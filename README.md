## Reportarr
___

A simple service that polls a healthcheck endpoint and updates discord with the health status using a spicy chat bot. 

This service can be run in a docker container using this command:
```
docker run -d --name reportarr \
  --restart unless-stopped \
  -e HealthCheck__BaseUrl=your_url \
  -e HealthCheck__Endpoint=your_endpoint_to_poll \
  -e HealthCheck__Interval=60000 \
  -e Discord__Token=token \
  -e Discord__ApplicationId=app_id \
  -e Discord__PublicKey=public_key \
  -e Discord__ChannelId=channel_id_where_status_updates_gp \
  -e Discord__AdminUserId=user_to_tag_with_messages \
  stewanoya/reportarr:latest
```

Otherwise this service can be run using `dotnet run`, just ensure to populate appsettings with above variables.
