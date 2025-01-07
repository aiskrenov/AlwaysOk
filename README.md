# AlwaysOk
A simple HTTP/S server that always returns 200 OK no matter the request. Can be used for blocked domains to avoid waiting on timeouts or breaking apps that depend on blocked resources. It also makes it obvious to recognize when a resource is blocked as the server will always return the `AlwaysOk` text.

Over HTTPS the server will use the Host header to generate a certificate signed by your authority on the fly with the current canonical name (CN) and add that certificate to the TPM/HSM module (using the current user's certificate store) where it can be used by the operating system for TLS communication.

## Principle of operation
![image](https://github.com/user-attachments/assets/bffc2b82-dd0b-4fcb-b2dd-fc6bc2ce8a89)


> [!NOTE]
> This setup assumes that you already have a DNS authority in your network setup that redirects all blocked domains to the host where AlwaysOk would be running.

> [!IMPORTANT]
> This software is provided as it is and is meant to be used in controlled private networks with the explicit approval of all parties involved. The author does not encourage or take any responsibility for malicious use of the provided software and any potential damage caused by it.

## Setting up your network
In order for this to work there are several steps that need to be configured

1. Generate a root certificate authority (CA). You can use OpenSSL to do that:
```bash
openssl req -x509 -nodes  \
  -newkey RSA:2048        \
  -keyout ca.key          \
  -days 36500             \
  -out ca.crt             \
  -subj '/C=AU/ST=NSW/L=Sydney/O=AlwaysOk/CN=AlwaysOk root CA'
```

2. For AlwaysOk we will need a PFX file so convert the certificate and the private key to a PKCS12:
```bash
openssl pkcs12 -export -out ca.pfx -inkey ca.key -in ca.crt
```

3. Trust the root CA on all hosts that will use AlwaysOk. You will need the ca.crt file for that:
   - [Windows](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/trusted-root-certification-authorities-certificate-store) - Open the start menu and select Manage Computer Certificates; Click on Trusted Root Certification Authorities; Right click on Certificates -> All Tasks -> Import... and follow the instructions. Alternatively, use `certutil` if deploying at scale - `certutil -addstore -f "ROOT" ca.crt`.
   - Linux - For Ubuntu and Debian: Copy your CA to a directory like /usr/local/share/ca-certificates/; Update the CA store with `sudo update-ca-certificates`. Refer to your Linux distribution documentation for specifics.
   - MacOS - Open the Keychain Access app (you need the Keychain Access, not the Passwords); Go to your System keychain and select the Certificates tab; Drag the CRT file here; Double-click on the newly imported certificate and select Always Trust under the Trust section. Alternatively, use `sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain ~/ca.crt`.
   - [iOS](https://support.apple.com/en-au/102390) - Upload the CRT file to the device via email, cloud storage or a messaging app; Open the file and install the certificate; Go to Settings > General > About > Certificate Trust Settings and under "Enable full trust for root certificates", turn on trust for the certificate. Alternatively, you can deploy at scale using UEM (Intune, Airwatch, Apple Configurator, etc.).
   - tvOS - Put the CRT file on a public HTTPS sever (any cloud storage with anonymous share link will do; local network shares won't work); Navigate to Settings > General > Privacy and select Share Apple TV Analytics; Press the Play/Pause button on the remote; This brings up a screen that lists the installed profiles along with an Add Profile option at the top; Select Add Profile and type in the share link (use your phone if you don't have a day to dial with the remote).

4. For the purposes of this document we will be using Docker to build and run AlwaysOk. Open a command shell and navigate to the root folder of the repository. Build the container using:
```bash
docker build -t alwaysok -f ./AlwaysOk/Dockerfile .
```

5. Since we are running this on a remote host let's tag the image and push it to a container registry. I use an internal private registry, but the DockerHub will do just fine:
```bash
docker image tag alwaysok registry.private/alwaysok:v1.0.0
```
and then:
```bash
docker push registry.private/alwaysok:v1.0.0
```

6. Not that we have an image we are happy with we can run it on the designated host. Note that we are running the container in the background (-d), using the default HTTP/S ports to expose the app and we want the container to run even if the host is rebooted:
```bash
sudo docker run -d  \
  -p 80:8080        \
  -p 443:8081       \
  --restart=always  \
  --name alwaysok   \
  registry.private/alwaysok:v1.0.0
```

7. Confirm that the container is now running and listening for requests:
```bash
sudo docker ps
```
and then
```bash
sudo docker logs alwaysok -n 100
```

> [!NOTE]
> In rare, but not unseen, cases some apps will use custom ports and you will need to expose those by modifying the Kestrel configuration and the `docker run` command.
