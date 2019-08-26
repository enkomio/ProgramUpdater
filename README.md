# Program Updater Framework
A framework to automatize the process of updating a program in an **efficent** and **secure way**. The updates are provided by a server component through and HTTP/HTTPS channel.

It was created with the following intents: 

* to be very easy to use and to integrate 
* to provide an high secure update process even on a not encrypted channel like HTTP (the integrity of all files is checked before being used)
* to be efficient, this means that if your new release just changed one file you don't need to download the full application but only the changed files
* to be autoconsistent, you don't need any other external software (web server, database, ...) to create an update solution

## Download

A pre-compiled file of the framework can be downloaded from the <a href="https://github.com/enkomio/ProgramUpdater/releases/latest">Release section</a>.

## Core Concepts

The framework can be used via the command line tools or by integrating it in your web application. In both cases the process to release a new update is composed of the following three steps:

* Create the metadata related to the new update
* Push the metadata to the update server (this step can be merged with the one above)
* Run the update program from the client

In order to setup an update process you need:

* A folder where all the metadata are saved
* To follow a naming convention for your update file (only zip file are supported for now). The convention is that the name must be something like: MyApplication.1.2.3.zip.
* To generate and distribute the encryption key (this process is done automatically on first server execution)

## Configuration File

All the command line options can also be specified in the given configuration file for each tools. The deafult name for the configuration file is **configuration.json** and it is in JSON format. If a command line value is specified it will take precedence over the value set in the configuration file.

# Examples

Below you can find some examples that should provide enough information to use the framework proficiently.

## Example 1

The goal of this example is to provide a full update process by only using the commant line utilities. We will suppose that we have four versions of our software and we want to release a new version 5.0. We will use the _update_ directory in order to store the information related to the updates of our software.

### Step 0 - Start up

If you have never used the framework to provide updates to your clients, it is a good practice to follow the _Step 1_ for each release of your software, starting from the oldest to the newest.

### Step 1 - Metadata Creation

The first step is to create the metadata, this is done with the **VersionReleaser.exe** tool. We run the following command:

````bash
VersionReleaser.exe --working-dir updates Examples\Example1\MyApplication.v5.0.zip
-=[ Version Releaser ]=-
Copyright (c) 2019 Enkomio

[INFO] 2019-08-09 19:26:45 - Analyze release file: MyApplication.v5.0.zip
[INFO] 2019-08-09 19:26:45 - Saving release metadata
[INFO] 2019-08-09 19:26:45 - Saving artifacts to update
[INFO] 2019-08-09 19:26:45 - Adding new file 'folder\file8.txt' as 77C6EC70B75CE3254B910DC6073DB04A61E2EB5273191F73B0AB539F6CAD43C2
[INFO] 2019-08-09 19:26:45 - Process completed
````
Now the metadata are created and the new artifacts are saved. You can exclude some files from the update process, this is very important for configuration file or local database. You can configure the patterns of the files to exclude in the **configuration.json** file. The current list can be found <a href="https://github.com/enkomio/ProgramUpdater/blob/master/Src/VersionReleaser/configuration.json">here</a>.

### Step 2 - Start the update server

Now you have to start the update server. The framework provides a program named **UpdateServer.exe** that will run a web server in order to accept update requests. You can do this with the following command:
````bash
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
````
The server recognizes that we defined five applications. It is also very important to take note of the *public key*. This value must be set in the client in order to ensure the integrity of the updates.

### Step 3 - Run the update client

The final step of this example is to update the client code by connecting to the server. In order to do this, it is necessary to specify the following information:

* The address of the update server
* The public key of the server
* The name of the project that must be updated

The first two information can be retrieved from the output of the server in the previous step. We suppose that the update must be installed in the current directory (a very common case if you distribute the update program togheter with your binary), if this is not the case you can change this value with the _--directory_ argument. You can now run the following command:
````bash
Updater.exe --project MyApplication --server-uri http://127.0.0.1 --server-key "RUNTNUIAAAABQa5NN74/BqJW7Ial8xj2D/QB32Dj7ZuMOmtfIfo4PiHuXD3QiM6xvOvEZbJ1vQPdjUignHYE7BCLdslEMYbCj4AA8QeSc9v7jc1X5cqKCL1tHaJc+B/MWp8sRXlL6wYUJj4bfcC3p/xEJZXeO/RUsO8gKA4KT0UAXsq0bExWRQr6Ioc="
-=[ Program Updater ]=-
Copyright (c) 2019 Enkomio

[INFO] 2019-08-13 14:31:28 - Found a more recent version: 5.0. Start update
[INFO] 2019-08-13 14:31:28 - Project 'MyApplication' was updated to version '5.0' in directory: .
````
If you now take a look at the current directory you will see that new files were created due to the update process.

## Example 2

The goal of this example is to show how to use the library in order to create a custom update. The result will be the same as the previous example. You can find the related files in the <a href="https://github.com/enkomio/ProgramUpdater/tree/master/Src/Examples/Example2">Example 2</a> folder.

### Step 1 - Metadata Creation

The most common case when you have to generate the metada for a new release is to use the command line utility. If for some reason you want to use the library you must use the **MetadataBuilder** class and specify the working directory where the metadata will be saved.

An example of usage is:
````csharp
var metadataBuilder = new MetadataBuilder(workspaceDirectory);
metadataBuilder.CreateReleaseMetadata(fileName);
````
### Step 2 - Start the update server

The framework provides a **WebServer** class that can be used to run the update server. The web server is based on the <a href="https://suave.io/">Suave</a> project. To run a web server you have to specify:

* The binding base URI
* The workspace directory where the metadata are stored
* The private key   
 
To generate a new pair of public and private keys you can use the **CryptoUtility.GenerateKeys** method. Find below an example of code that starts a web server.
````csharp
var (publicKey, privateKey) = CryptoUtility.GenerateKeys();
var server = new WebServer(this.BindingUri, this.WorkspaceDirectory, privateKey);	
````
### Step 3 - Implement the update client

The last step is to integrate the update client in your solution. In this case you need the following information:

* The server base URI
* The server public key
* The name of the project that you want to update
* The current project version
* The destination directory where the update must be installed

All information should alredy know if you followed the Step 2. Now you can update your client with the following code:
````csharp
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
````
## Example 3

The goal of this example is to show how to customize the web server. Often the update must be provided only to clients that have the needed authorization, in this example we will see how to authenticate the update requests. You can find the example files in the <a href="https://github.com/enkomio/ProgramUpdater/tree/master/Src/Examples/Example3">Example 3</a> folder.


### Step 0 - Installing dependency

The framework uses <a href="https://suave.io/">Suave</a> in order to implements the web server. In case of simple use of the ProgramUpdater framework, you don't have to worry about it but in this example it is necessary to reference it in order to use its classes. You can use <a href="https://fsprojects.github.io/Paket/">Paket</a> to reference it or add it via <a href="https://www.nuget.org/packages/Suave">NuGet</a>.

### Step 1 - Metadata Creation

See *Example 1* Step 1

### Step 2 - Start the update server

For this example we will create a sub-class of the **WebServer** framework class and we override the **Authenticate** method in order to verify the credentials that will be sent by the updater.

Below you can find the relevant code that checks if the credentials are correct:
````csharp
var formParameters = Encoding.UTF8.GetString(ctx.request.rawForm).Split('&');
var username = String.Empty;
var password = String.Empty;

foreach(var parameter in formParameters)
{
	var nameValue = parameter.Split('=');
	if (nameValue[0].Equals("Username", StringComparison.OrdinalIgnoreCase))
	{
		username = nameValue[1];
	}
	else if (nameValue[0].Equals("Password", StringComparison.OrdinalIgnoreCase))
	{
		password = nameValue[1];
	}
}

return 
	username.Equals(AuthenticatedWebServer.Username, StringComparison.Ordinal) 
	&& password.Equals(AuthenticatedWebServer.Password, StringComparison.Ordinal);
````		
<a href="https://github.com/enkomio/ProgramUpdater/blob/master/Src/Examples/Example3/AuthenticatedWebServer.cs#L37">Here</a> you can find the full source code of the **AuthenticatedWebServer** class.

### Step 3 - Implement the update client

In this case the difference with the previous example is that we have to authenticate to the server. This is an easy step if we know the username and password. We just have to add these info to the update request. This is easily done with the following code:
````csharp
// add username and password to the update request
updater.AddParameter("username", AuthenticatedWebServer.Username);
updater.AddParameter("password", AuthenticatedWebServer.Password);
````
The specified parameters will be added to the update request and will be used to verify the authentication. At his time it is possible to specify only POST parameters.

<a href="https://github.com/enkomio/ProgramUpdater/blob/master/Src/Examples/Example3/Client.cs#L15">Here</a> you can find the full source code of the **Client** class.

## Example 4

The goal of this example is to provide a flexible update method by invoking an external program to do the update. Often the update method is not just a matter of copy the files to the destination directory but other, more complex, tasks must be done. The full source code of this example can be find in the <a href="https://github.com/enkomio/ProgramUpdater/tree/master/Src/Examples/Example4">Example 4 folder</a>.

In version 1.1 was released a new feature that allows to invoke an external program in order to do the installation. The framework provides an **Installer** program that copy the files to a destination directory. Using this approach is the suggested one, since it will avoid to have update problems when you have to update the current running program (you cannot write on a file associated to a running process). In order achieve this, when an external Installer is used, the update process is terminated in order to avoid conflict. If you want to be sure that the parent exited just wait on the mutex name composed from the arguments hashes, to know more take a look at the <a href="https://github.com/enkomio/ProgramUpdater/blob/master/Src/Installer/Program.fs#L51">mutex name generation code<a/>.

Of course you can use your own installer program, you have just to add it to the configuration (we will see how to do it). The only rules that must be respected are:

* The name of the installer program must be **Installer.exe**
* It accepts the following arguments: 
    * **--source** that is the directory where the new files  are stored 
    * **--dest** that is the directory that must be updated with the new files
    * **--exec** the full path of an application to run after that the installation process is completed. If empty no program will be invoked
    * **--args** an optional srgument string to pass to the program to invoke after installation.

### Step 1 - Metadata Creation

See *Example 1* Step 1.

### Step 2 - Start the update server

This step is very similar to *Example 2* Step 2. The main difference is that you have to specify the directory where your installer is stored. All the files in this directory will be copied in the update package sent to the client. In the listing below you can see an example that use the **Installer.exe** provided by the framework:

````csharp
// set the installer path
var installerPath = Path.GetDirectoryName(typeof(Installer.Program).Assembly.Location);
_server.WebServer.InstallerPath = installerPath;
````
The **Installer** program from the framework by default will start again the main process with the specified arguments.

For security reason the framework will add the integrity info about the installer inside the update package. These info will be checked by the client before to invoke the installer.

### Step 3 - Run the update client

See Step 3 of previous examples.

# Security

The update process use **ECDSA** with **SHA-256** in order to ensure the integrity of the update. The *public* and *private* keys are automatically generated on first start and saved to local files (*public.txt* and *private.txt*). 

## Exporting the private key

In order to protect the private key from an attacker that is able to read aribrary files from your filesystem, the key is AES encrypted with parameters that are related to the execution environment (MAC address, properties of the installed HDs). This means that you cannot just copy the private key file from one computer to another, since it will not work. If you want to obtain the clear private key you have to export it by executing the following command:

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
