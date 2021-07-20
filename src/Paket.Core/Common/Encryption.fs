namespace Paket.Core.Common

open System
open System.IO
open System.Net.Http
open System.Security.Cryptography
open System.Text
open Paket
open Paket.Logging

type private AesKey<'T> = private AesKey of 'T
type private AesIV<'T> = private AesIV of 'T

type PlainTextPassword = PlainTextPassword of string
type AesEncryptedPassword = private AesEncryptedPassword of string
type DPApiEncryptedPassword = private DPApiEncryptedPassword of string

[<RequireQualifiedAccess>]
type EncryptedPassword =
    | Aes of AesEncryptedPassword
    | DPApi of DPApiEncryptedPassword
    with
    override x.ToString() =
        match x with
        | Aes (AesEncryptedPassword password) | DPApi (DPApiEncryptedPassword password) -> password

type AesSalt = private AesSalt of string
type DPApiSalt = private DPApiSalt of string

[<RequireQualifiedAccess>]
type Salt =
    | Aes of AesSalt
    | DPApi of DPApiSalt
    with
    override x.ToString() =
        match x with
        | Aes (AesSalt salt) | DPApi (DPApiSalt salt) -> salt

[<RequireQualifiedAccess>]
module private AesSalt =
    let [<Literal>] saltSeparator = "SALT_SEPARATOR"

    let private toBytes (AesKey keyString, AesIV ivString) =
        (AesKey << Convert.FromBase64String) keyString,
        (AesIV << Convert.FromBase64String) ivString

    let encode (AesKey key: AesKey<byte[]>,AesIV iv: AesIV<byte[]>) =
        [ key; iv ]
        |> Seq.map Convert.ToBase64String
        |> String.concat saltSeparator
        |> AesSalt

    let decode (AesSalt saltString) =
        match saltString.Split([|saltSeparator|], StringSplitOptions.None)  with
        | [|keyString;ivString|] -> (AesKey keyString, AesIV ivString)
        | _ -> failwith "Should never happen"

    let (|IsAesSalt|_|) (str: string) =
        if str.Contains saltSeparator then
            Some <| AesSalt str
        else
            None

[<RequireQualifiedAccess>]
module Aes =
    let private encryptString (password: string) =
        let writePassword (cryptoStream: CryptoStream) =
            use streamWriter = new StreamWriter(cryptoStream)
            streamWriter.Write(password)

        use aes = Aes.Create()
        let encryptor = aes.CreateEncryptor()
        use memoryStream = new MemoryStream()
        use cryptoStream = new CryptoStream(memoryStream,encryptor,CryptoStreamMode.Write)
        writePassword cryptoStream
        let encryptedPassword = memoryStream.ToArray()
        encryptedPassword,aes.Key,aes.IV

    let private decryptBytes (encryptedBytes: byte[]) key iv =
        use aes = Aes.Create()
        aes.Key <- key
        aes.IV <- iv
        let decryptor = aes.CreateDecryptor()
        use memoryStream = new MemoryStream(encryptedBytes)
        use cryptoStream = new CryptoStream(memoryStream,decryptor,CryptoStreamMode.Read)
        use streamReader = new StreamReader(cryptoStream)
        let password = streamReader.ReadToEnd()
        password

    let private serialize password (key: byte[]) (iv: byte[]) =
        (AesEncryptedPassword << Convert.ToBase64String) password,
        AesSalt.encode (AesKey key, AesIV iv)

    let private deserialize (AesEncryptedPassword password) aesSalt =
        let AesKey key, AesIV iv = AesSalt.decode aesSalt

        Convert.FromBase64String password,
        Convert.FromBase64String key,
        Convert.FromBase64String iv

    let encrypt (PlainTextPassword password) =
        encryptString password
        |||> serialize

    let decrypt encryptedPassword aesSalt =
        deserialize encryptedPassword aesSalt
        |||> decryptBytes
        |> PlainTextPassword

[<RequireQualifiedAccess>]
module private DPApiSalt =
    let private fillRandomBytes =
        let provider = RandomNumberGenerator.Create()
        (fun (b:byte[]) -> provider.GetBytes(b))

    let encode bytes =
        bytes
        |> Convert.ToBase64String
        |> DPApiSalt

    let getRandomSalt() =
        let saltSize = 8
        let saltBytes = Array.create saltSize ( new Byte() )
        fillRandomBytes(saltBytes)
        saltBytes

[<RequireQualifiedAccess>]
module DPApi =
    let encrypt (PlainTextPassword password) =
        let salt = DPApiSalt.getRandomSalt()
        let encryptedPassword =
            try
                ProtectedData.Protect(Encoding.UTF8.GetBytes password, salt, DataProtectionScope.CurrentUser)
            with | :? CryptographicException as e ->
                if verbose then
                    verbosefn "could not protect password: %s\n for current user" e.Message
                ProtectedData.Protect(Encoding.UTF8.GetBytes password, salt, DataProtectionScope.LocalMachine)
        encryptedPassword |> Convert.ToBase64String |> DPApiEncryptedPassword,
        salt |> DPApiSalt.encode

    let decrypt (encryptedPassword : string) (salt : string)  =
        ProtectedData.Unprotect(Convert.FromBase64String encryptedPassword, Convert.FromBase64String salt, DataProtectionScope.CurrentUser)
        |> Encoding.UTF8.GetString
        |> PlainTextPassword

[<RequireQualifiedAccess>]
module Crypto =
    let encrypt plainTextPassword = 
        if isWindows then
            let dpApiPassword, dpApiSalt = DPApi.encrypt plainTextPassword
            (EncryptedPassword.DPApi dpApiPassword, Salt.DPApi dpApiSalt)
        else
            let aesPassword, aesSalt = Aes.encrypt plainTextPassword
            (EncryptedPassword.Aes aesPassword, Salt.Aes aesSalt)

    let decrypt password salt =
        match salt with
        | AesSalt.IsAesSalt salt ->
            Aes.decrypt (AesEncryptedPassword password) salt
        | _ ->
            DPApi.decrypt password salt
