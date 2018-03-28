module Paket.AuthService

    let GetGlobalAuthenticationProvider source =
        AuthProvider.combine
            [ ConfigFile.GetAuthenticationProvider source
              CredentialProviders.GetAuthenticationProvider source ]
