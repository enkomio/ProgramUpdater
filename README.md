# Program Updater Framework
A framework to automatize the process of updating a program in an efficent and secure way. The updates are provided by a server component through and HTTP/HTTPS channel.

It was created with the following intents: 

* to be very easy to use and to integrate 
* to provide an high security process
* to be efficient, if your new release just changed one file you don't need to download the full application but only the changed files
* to be autoconsistent, you don't need any other external software (web server, database, ...)

## Core Concepts

The framework can be used via the command line tools or by integrating it in your web application. In both case, the process to release a new update is composed of the following three steps:

* Create the metadata related to the new update
* Push the metadata to the update server (this step can be merged with the above one)
* Run the updated from the client

In order to setup an update process you need:

* A folder where all the metadata are saved
* To follow a naming convention for your update file (only zip file are supported for now)
* Generate and distribute the encryption key (this process is done automatically on first start)

Find below some examples that describe how to use the framework.

## Configuration File

All the command line options can also be specified in the given configuration file for each tool. The deafult name for the configuration file is **configuration.json** and it is in JSON format. If a command line value is specified it will take precedence over the value set in the configuration file.

# Example

Below you can find some examples that should provide enough information to use the framework proficiently.

## Example 1

The goal of this example is to provide a full update process by only using the commant line utilities. We will suppose that we have four versions of our software and we want to release a new version 5.0. We will use the _update_ directory in order to store the information related to the updates of our software.

### Step 0 - Start up

If you have never used the framework to provide updates to your clients, it is a good practice to follow the _Step 1_ for each release of your software, starting from the oldest to the newest.

### Step 1 - Metadata Creation

The first step is to create the metadata, this is done with the **VersionReleaser.exe** tool. We run the following command:

	VersionReleaser.exe --working-dir updates Examples\Example1\MyApplication.v5.0.zip
	-=[ Version Releaser ]=-
	Copyright (c) 2019 Enkomio

	[INFO] 2019-08-09 19:26:45 - Analyze release file: MyApplication.v5.0.zip
	[INFO] 2019-08-09 19:26:45 - Saving release metadata
	[INFO] 2019-08-09 19:26:45 - Saving artifacts to update
	[INFO] 2019-08-09 19:26:45 - Adding new file 'folder\file8.txt' as 77C6EC70B75CE3254B910DC6073DB04A61E2EB5273191F73B0AB539F6CAD43C2
	[INFO] 2019-08-09 19:26:45 - Process completed
	
Now the metadata are created and the new artifacts are saved. You can exclude some file from the update process, this is very important for configuration file or local database. You can configure the pattern of the file to exclude in the **configuration.json** file. The current list can be found <a href="https://github.com/enkomio/ProgramUpdater/blob/master/Src/VersionReleaser/configuration.json">here</a>.

### Step 2 - Start the update server

Now you have to start the update server. The framework provides a program named **UpdateServer.exe** that will run a web server in order to accept update requests. You can do this with the following command:

	UpdateServer.exe --working-dir updates
	-=[ Version Releaser ]=-
	Copyright (c) 2019 Enkomio

	[INFO] 2019-08-10 15:06:48 - Encryption keys not found. Generating them
	[INFO] 2019-08-10 15:06:48 - Encryption keys created and saved to files. The public key must be distributed togheter with the updater
	[INFO] 2019-08-10 15:06:48 - Public key: RUNTNUIAAAABQa5NN74/BqJW7Ial8xj2D/QB32Dj7ZuMOmtfIfo4PiHuXD3QiM6xvOvEZbJ1vQPdjUignHYE7BCLdslEMYbCj4AA8QeSc9v7jc1X5cqKCL1tHaJc+B/MWp8sRXlL6wYUJj4bfcC3p/xEJZXeO/RUsO8gKA4KT0UAXsq0bExWRQr6Ioc=
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 1.0
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 2.0
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 3.0
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 4.0
	[INFO] 2019-08-10 15:06:48 - Loaded project MyApplication version 5.0
	[17:06:48 INF] Smooth! Suave listener started in 86.698ms with binding 127.0.0.1:80
	
The server recognizes that we defined five applications. It is also very important to take note of the *public key*. This value must be set in the client in order to ensure the integrity of the updates.

### Step 3 - Run the update client

The final step of this example is to update the client code by connecting to the server. In order to do this, it is necessary to specify the following information:

* The address of the update server
* The public key of the server
* The name of the project that must be updated

The first two information can be retrieved from the output of the server in the previous step. We suppose that the update must be installed in the current directory (a very common case if you distribute the updater program togheter with your binary), you can change this value with the _--directory_ argument. You can now run the following command:

	Updater.exe --project MyApplication --server-uri http://127.0.0.1 --server-key "RUNTNUIAAAABQa5NN74/BqJW7Ial8xj2D/QB32Dj7ZuMOmtfIfo4PiHuXD3QiM6xvOvEZbJ1vQPdjUignHYE7BCLdslEMYbCj4AA8QeSc9v7jc1X5cqKCL1tHaJc+B/MWp8sRXlL6wYUJj4bfcC3p/xEJZXeO/RUsO8gKA4KT0UAXsq0bExWRQr6Ioc="
	-=[ Program Updater ]=-
	Copyright (c) 2019 Enkomio

	[INFO] 2019-08-13 14:31:28 - Found a more recent version: 5.0. Start update
	[INFO] 2019-08-13 14:31:28 - Project 'MyApplication' was updated to version '5.0' in directory: .
	
If you now take a look at the current directory you will see the new files that were created due to the update process.

## Example 2

The goal of this example is to show how to use the library in order to create a custom update. The result will be the same as the previous example. You can find the related files in the <a href="https://github.com/enkomio/ProgramUpdater/tree/master/Src/Examples/Example2">Example 2</a> folder.

### Step 1 - Metadata Creation

The most common case when you have to generate the metada for a new release is to use the command line utility. If for some reason you want to use the library you must use the **MetadataBuilder** class and specify the working directory where the metadata will be saved.

An example of usage is:

	var metadataBuilder = new MetadataBuilder(workspaceDirectory);
	metadataBuilder.CreateReleaseMetadata(fileName);
	
### Step 2 - Start the update server

The framework provides a **WebServer** class that can be used to run the update server. The web server is based on the Suave project. To run a web server you have to specify:

* The binding base URI
* The workspace directory
* The private key   
 
To generate a new pair of public and private keys you can use the **CryptoUtility.GenerateKeys** method. Find below an example of code that start a web server.

	var (publicKey, privateKey) = CryptoUtility.GenerateKeys();
	var server = new WebServer(this.BindingUri, this.WorkspaceDirectory, privateKey);	
	
### Step 3 - Implement the update client

The last step is to integrate the updater client in your solution. In this case you need the following information:

* The server base URI
* The server public key
* The name of the project that you want update
* The current project version
* The destination directory where the update must be installed

All information should alredy know if you followed the Step 2. Now you can update your client with the following code:

	var applicationVersion = new Version(3, 0);            
	var updater = new Updater(serverBaseUri, applicationName, applicationVersion, destinationDirectory, serverPublicKey);

	var latestVersion = updater.GetLatestVersion();
	if (latestVersion > applicationVersion)
	{
		var updateResult = updater.Update(applicationVersion);
		if (updateResult.Success)
		{                    
			// Update ok
		}
		else
		{
			// Error
		}
	}
	
## Example 3

The goal of this example is to show how to customize the web server. Often the update must be provided only to clients that have the needed authorization, in this example we will see how to authorize update requests. The result will be the same as the previous example, indeed most of the code will be pretty much the same except the server code. You can find the related files in the <a href="https://github.com/enkomio/ProgramUpdater/tree/master/Src/Examples/Example3">Example 3</a> folder.

### Step 1 - Metadata Creation

See Example 2 Step 1

### Step 2 - Start the update server

// TODO

### Step 3 - Implement the update client

In this case the difference with the previous example is that we have to authenticate to the server.

// TODO

# Security

The update process use ECDSA with SHA-256 in order to ensure the integrity of the update. The public and private keys are automatically generated on first start and saved to local files. 

## Exporting private key

In order to protect the private key from an attacker that is able to read aribrary files from your filesystem, the key is AES encrypted with parameters that are related to the execution environment (like MAC address, installed HDs, ...). This means that you cannot just copy the private key file from one computer to another, since it will not work. If you want to obtain the clear private key you have to export it by executing the following command:

	UpdateServer.exe --export-key clean-private-key.txt
	-=[ Version Releaser ]=-
	Copyright (c) 2019 Enkomio

	[INFO] 2019-08-09 13:45:18 - Public key: RUNTNkIAAAAAQSTd1xnmvNHa25Z4ENfcXeTlktWZdnABFn/jwcx/KOBX44qZOY/aEp1oXxfhcXZX26Uy5c2P1FZlu5yswPAgqxUBXpxjSyCSYnyKODNpLw0sEqD+L3xcJLIv/3s4vgFaCwIDNiqqn8WWahvsYsu0o41IgMYwjOO4QhsL16Xai+beAEEBBRoWkZJSZR+vB7Vi/Trw7C5kNsPwy5TxK9Fd+ibyrAyewvftI1SWAcEO6OIh9G+bSEkXDPoS77faGYMotbcKhQU=
	[INFO] 2019-08-09 13:45:18 - Private key exported to file: clean-private-key.txt
  
## Importing private key

If you want to import a private key that was exported from another server you can do it by run the following command:

	UpdateServer.exe --import-key clean-private-key.txt
	-=[ Version Releaser ]=-
	Copyright (c) 2019 Enkomio

	[INFO] 2019-08-09 13:47:40 - Public key: RUNTNkIAAAABtk8oMxMbWwWeBVKGckyVK4C9oOdyKSy6/WNG/6763CUEZk+mCf2zgGBViDpPu2N/Crh99rDK2WGsE2b9nYqaq7AA7caRHqcPLXns+aPqjk1teFI9c9+QnU78WOrd2UMKF3CuD2xccvjKATon+3GHBWeJtqZNvXSu8blWmFENmkIMS60BXl2pXb7fPuTXRaSyj6Dtb/IY4CY2rftroIJx1B3g28UHs0cVXWK+pi/DOkWJMb4EspodK9caIjwLxwf1HF3LnVc=
	[INFO] 2019-08-09 13:47:41 - Private key from file 'clean-private-key.txt' imported. Be sure to set the public key accordingly.
  
This command will read and save the private key in an encrypted form with the new parameters of the new server. You have also to copy the public key to the server.
