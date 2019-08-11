# Program Updater Framework
A framework to automatize the process of updating a program in an efficent and secure way. The updates are provided by a server component through and HTTP/HTTPS channel.

It was created with the following intents: 

* to be very easy to use and to integrate 
* to provide an hish security process
* to be autoconsistent, you don't need any other external software (web server, database, ...)

## Usage

The framework can be used via the command line tools or by integrating it in your web application. In both case, the step to release a new update is composed of the following three steps:

* Create the metadata related to the new update
* Push the metadata to the update server (this step can be merged with the above one)
* Run the updated from the client

### Core concepts

In order to setup an update process you need:

* A folder where are the metadata are saved
* Following a naming convention for you update file (only zip file are supported for now)
* Generate and distribute the encryption key (this process is done automatically on first start)

Find below some examples that describe how to use the framework.

### Example 1

The goal of this example is to provide a full update process by only using the commant line utilities. We will suppose that we have four version of our software and we just released a newv version, 5.0. We will use the _update_ directory in order to store the information related to the updates of our software.

#### Step 0 - Start up

If you have never used the framework to provide updates to your clients, is good practice to follow the _Step 1_ for each release of your software, starting from the oldest to the newest.

#### Step 1 - Metadata Creation

The first step is to create the metadata, this is done with the _VersionReleaser.exe_ tool. We run the following command:

	VersionReleaser.exe --working-dir updates Examples\Example1\MyApplication.v5.0.zip
	-=[ Version Releaser ]=-
	Copyright (c) 2019 Enkomio

	[INFO] 2019-08-09 19:26:45 - Analyze release file: MyApplication.v5.0.zip
	[INFO] 2019-08-09 19:26:45 - Saving release metadata
	[INFO] 2019-08-09 19:26:45 - Saving artifacts to update
	[INFO] 2019-08-09 19:26:45 - Adding new file 'folder\file8.txt' as 77C6EC70B75CE3254B910DC6073DB04A61E2EB5273191F73B0AB539F6CAD43C2
	[INFO] 2019-08-09 19:26:45 - Process completed
	
Now the metadata are created and the new artifacts are saved.

#### Step 2 - Start update server

Now you have to start the update server. The framework provides an utility that will run a web server in order to accept update requests. You can do this with the following command:

	UpdateServer.exe --working-dir updates
	-=[ Version Releaser ]=-
	Copyright (c) 2019 Enkomio

	[INFO] 2019-08-10 15:06:48 - Encryption keys not found. Generating them
	[INFO] 2019-08-10 15:06:48 - Encryption keys created and saved to files. The public key must be distributed togheter with the updater
	[INFO] 2019-08-10 15:06:48 - Public key: RUNTNkIAAAAAOhEpGNVJ7p3RtrhaJG2L+B8Ov4rCjN+DllHhD6Ang3l/ca8YiSCsBFoJD1W0CRsUcbFX3hrEUTmuz7166Ao7e8IBOmLcB1YAPTzKbp5RlQBi4ghvi/pwQChWW5QkWIlZVevwJY1/ghZtfLmCnKZDCiv8pE75QG/lSARKf3otVZXXBKYBRbZMwyWtBUp/sCNS4Dq/0Syi8ssr3XUlpJatmnAguVfKHCe746eVy82alRnT3nepAopYdclFhKdGJxdEBgEd0JM=
	[INFO] 2019-08-10 15:06:48 - Private key first bytes: RUNTNU
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 1.0
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 2.0
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 3.0
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 4.0
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 5.0
	[17:06:48 INF] Smooth! Suave listener started in 86.698ms with binding 127.0.0.1:80
	
The server recognized that we defined five applications. It is also very important to take note ofthe *public key*. This value must be set in the update client in order to ensure the integrity of the updates.

#### Ste 3 - Run the update client

The final step of this example is to update the client code by connecting to the server. In order to do this, it is necessary to specify the following information:

* The address of the update server
* The public key of the server
* The directory where the update must be installed

The first two information can be retrieved from the output of the server in the previous step. For the last information we suppose that is the current directory (a very common case if you distribute the updater program togheter with your binary). You can now run the following command:

### Example 2

## Security

The update process use ECDSA with SHA-256 in order to ensure the integrity of the update. The public and private keys are automatically generated on first start and saved to local files. 

### Exporting private key

In order to protect the private key from an attacker that is able to read aribrary file from your filesystem, the key is AES encrypted with parameters that are related to the execution environment (like MAC address, HD features, ...). This mean that you cannot just copy the private key file from one computer to another, since it will not work. If you want to obtain the clear private key you have to export it by issuing the following command:

	UpdateServer.exe --export-key clean-private-key.txt
	-=[ Version Releaser ]=-
	Copyright (c) 2019 Enkomio

	[INFO] 2019-08-09 13:45:18 - Public key: RUNTNkIAAAAAQSTd1xnmvNHa25Z4ENfcXeTlktWZdnABFn/jwcx/KOBX44qZOY/aEp1oXxfhcXZX26Uy5c2P1FZlu5yswPAgqxUBXpxjSyCSYnyKODNpLw0sEqD+L3xcJLIv/3s4vgFaCwIDNiqqn8WWahvsYsu0o41IgMYwjOO4QhsL16Xai+beAEEBBRoWkZJSZR+vB7Vi/Trw7C5kNsPwy5TxK9Fd+ibyrAyewvftI1SWAcEO6OIh9G+bSEkXDPoS77faGYMotbcKhQU=
	[INFO] 2019-08-09 13:45:18 - Private key first bytes: RUNTNU
	[INFO] 2019-08-09 13:45:18 - Private key exported to file: clean-private-key.txt
  
### Importing private key

If you want to import a private key that was exported from another server you have to run the following command:

	UpdateServer.exe --import-key clean-private-key.txt
	-=[ Version Releaser ]=-
	Copyright (c) 2019 Enkomio

	[INFO] 2019-08-09 13:47:40 - Public key: RUNTNkIAAAABtk8oMxMbWwWeBVKGckyVK4C9oOdyKSy6/WNG/6763CUEZk+mCf2zgGBViDpPu2N/Crh99rDK2WGsE2b9nYqaq7AA7caRHqcPLXns+aPqjk1teFI9c9+QnU78WOrd2UMKF3CuD2xccvjKATon+3GHBWeJtqZNvXSu8blWmFENmkIMS60BXl2pXb7fPuTXRaSyj6Dtb/IY4CY2rftroIJx1B3g28UHs0cVXWK+pi/DOkWJMb4EspodK9caIjwLxwf1HF3LnVc=
	[INFO] 2019-08-09 13:47:40 - Private key first bytes: RUNTNU
	[INFO] 2019-08-09 13:47:41 - Private key from file 'clean-private-key.txt' imported. Be sure to set the public key accordingly.
  
This command will read and saved the private key in an encrypted form with the new parameters of the new server. You have also to copy to the server the public key.
