# ProgramUpdater
A framework to automatize the process of updating a program in an efficent and secure way. The updates are provided by a server component through and HTTP/HTTPS channel.

It was created in order to be very easy to use and to integrate without sacrifice security.

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
