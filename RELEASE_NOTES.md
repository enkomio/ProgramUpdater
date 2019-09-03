### 1.2.0 - 29/08/2019
* Added utility to generate public and private key from command line
* Added timer to avoid to restart program when a new update is available
* Moved to Bouncy Castle for encryption in order to be supported on Mono too
* Fixed minor error

### 1.1.0 - 28/08/2019
* Improved installer by invoking an external program to avoid conflict on copy.
* Exporting and importing the private key now require a password to save the value in an encrypted form. This will ensure that the private key is never stored in clear text.
* Minor bug fixing and code improvement.

### 1.0.0 - 19/08/2019
* First Release.
