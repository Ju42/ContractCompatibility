{
  description = "ContractCompatibility";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-24.11";
  };

  outputs = { self, nixpkgs }:
  let
    system = "x86_64-linux";
    pkgs = import nixpkgs { inherit system; config.allowUnfree = true; };
    commonPackages = with pkgs; [
      dotnet-sdk_9
      mono
    ];
  in
  {
    devShells.${system} = {
      default = pkgs.mkShell {
        nativeBuildInputs = with pkgs; [
          jetbrains.rider
        ] ++ commonPackages;
      };
      ci = pkgs.mkShell {
        nativeBuildInputs = commonPackages;
      };
    };
  };
}

