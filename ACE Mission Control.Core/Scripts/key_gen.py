import sys
import os
import subprocess
from cryptography.hazmat.primitives import serialization as crypto_serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.backends import default_backend as crypto_default_backend


def generate_keys(directory, passphrase=None):
    if directory is ".":
        directory = "keys/"
        if not os.path.isdir("keys"):
            os.makedirs("keys")

    print("Making keys in: " + directory)
    print("Private key is " + ("UNENCRYPTED" if passphrase is None else "ENCRYPTED"))

    key = rsa.generate_private_key(
        backend=crypto_default_backend(),
        public_exponent=65537,
        key_size=2048
    )

    if passphrase is None:
        private_key = key.private_bytes(
            encoding=crypto_serialization.Encoding.PEM,
            format=crypto_serialization.PrivateFormat.TraditionalOpenSSL,
            encryption_algorithm=crypto_serialization.NoEncryption())
    else:
        private_key = key.private_bytes(
            encoding=crypto_serialization.Encoding.PEM,
            format=crypto_serialization.PrivateFormat.TraditionalOpenSSL,
            encryption_algorithm=crypto_serialization.BestAvailableEncryption(bytes(passphrase, 'utf8')))

    priv_key_file = open(directory + "id_rsa", "wb")
    priv_key_file.write(private_key)
    priv_key_file.close()

    public_key = key.public_key().public_bytes(
        crypto_serialization.Encoding.OpenSSH,
        crypto_serialization.PublicFormat.OpenSSH
    )

    pub_key_file = open(directory + "id_rsa.pub", "wb")
    pub_key_file.write(public_key)
    pub_key_file.close()

    args = ['icacls',
            os.path.abspath(directory),
            '/t',
            '/grant:rx',
            'ALL APPLICATION PACKAGES:RX']
    subprocess.check_call(args)

    print("All done")


def main():
    num_args = len(sys.argv)
    if num_args == 1:
        while True:
            passphrase = input("Enter a passphrase for the key: ")
            if len(passphrase) == 0:
                generate_keys(".")
                break
            else:
                os.system("cls")
                passphrase2 = input("Please re-enter the passphrase: ")
                if passphrase == passphrase2:
                    generate_keys(".", passphrase)
                    break
                else:
                    os.system("cls")
                    print("Passphrase did not match")
    elif num_args == 2:
        generate_keys(str(sys.argv[1]))
    elif num_args >= 3:
        generate_keys(str(sys.argv[1]), str(sys.argv[2]))


if __name__ == '__main__':
    main()
