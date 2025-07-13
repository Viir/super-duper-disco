module CompilationInterface.GenerateJsonConverters exposing (..)

{-| For documentation of the compilation interface, see <https://github.com/pine-vm/pine/blob/main/guide/customizing-elm-app-builds-with-compilation-interfaces.md#compilationinterfacegeneratejsonconverters-elm-module>
-}

import HttpApi
import Json.Decode


jsonDecodeClientRequest : Json.Decode.Decoder HttpApi.ClientRequest
jsonDecodeClientRequest =
    Json.Decode.fail "The compiler replaces this declaration."
