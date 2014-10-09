set PASSWORD=the-password-entered-when-exporting-the-pfx-from-the-certificate-store
set PASSWORD_PFX=code-sign.pfx
set NO_PASSWORD_PFX=code-sign-nopass.pfx

openssl pkcs12 -in %PASSWORD_PFX% -nodes -out temp.pem -password pass:%PASSWORD%

openssl pkcs12 -export -in temp.pem -out %NO_PASSWORD_PFX% -password pass:

rm temp.pem
