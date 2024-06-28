# Jumpserver用户登录信息按月导出Excel统计表并上传SMB共享



## 使用说明：

1. 在config.json配置文件里可以预先定义服务器信息，SMB信息，本地信息等，debug模式使用config.dev.json文件

   ```
   {
     "JmsServer": [
       {
         "name": "Jumpserver A",
         "baseUri": "http://192.168.1.1",
         "token": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
       },
       {
         "name": "Jumpserver B",
         "baseUri": "http://serverb.xxx.com",
         "token": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
       },
       {
         "name": "Jumpserver C",
         "baseUri": "https://serverc.xxx.com",
         "token": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
       }
     ],
     "SmbServer": {
       "host": "192.168.123.1",
       "netBiosOverTCP": true,
       "port": 445,
       "domain": "",
       "username": "smbuser",
       "password": "smbpass",
       "shareName": "cifs_server",
       "sharePath": "巡检报告\\堡垒机",
       "overwrite": false
     },
     "BackupOption": {
       "monthFromOffset": -12,
       "monthToOffset": -1,
       "localExportPath": "export",
       "overwrite": false
     }
   }
   ```

   

