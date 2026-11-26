namespace Vita.Core.TitleRules;

internal static class DoukokuSoshiteRecipe
{
    public static readonly EmbeddedPayloadRecipe Value = new(".Payload.", "DoukokuSoshiteKoreanPatch.dll",
    [
        new("data/data.psarc",
        [
            new("data.psarc.vcdiff", "c084ea4a168204ad8dcac9b7dfcbb3510c301db47e844c7ce938ce33d0355d3d", "0a0fccdd7f321c2ac0ad454df7931c93d9a5046f4c7aef54277ad5bbce3cb1e4", 667161589)
        ]),
        new("data/font/NotoSansCJKjp-Medium.otf",
        [
            new("NotoSansCJKjp-Medium.otf.vcdiff", "bcda0bcc22bab28ea4fedae800bfbf9ec76d71cc3b9f851779a35b7e438a839d", "1fc269ba61a22e9c63e67ae6cb4e2aa0320ba4bd4df04e91d86c1f012d6c5495", 16512760)
        ]),
        new("data/font/NotoSerifCJKjp-Medium.otf",
        [
            new("NotoSerifCJKjp-Medium.otf.vcdiff", "004cfc95eede14c5902ac3b648a39665e307c04f307455db2ae421164721614f", "109c55f8238da123849868e80869860846b0f7a15c4c97fb894b16e46cb1c374", 23890564)
        ]),
        new("data/movie/prologue_psv.mp4",
        [
            new("prologue_psv.mp4.vcdiff", "339c9fb4e47ace69893f0df892615257f1546f6d0932f949f0ea0d9011736d19", "21c9878bb4e112469d60479e8c001802cf2b95c7df7107b2ef424c512176a57d", 43579207),
            new("prologue_psv.protected.mp4.vcdiff", "fcfe02b6888186d377c838eb3bd33ce352df15ef03c71d536532067edfd46380", "21c9878bb4e112469d60479e8c001802cf2b95c7df7107b2ef424c512176a57d", 43579207)
        ]),
        new("eboot.bin",
        [
            new("eboot-1.00.bin.vcdiff", "5cf65ded474ccd9a55e855c39e9f1f5fd0514af90c1bc9ba58e2f9a3e2fdad53", "e5fdeec5adf1fbdc361dcce2d9d6350639d98f1b46de224ab3e7377079e408c5", 409551),
            new("eboot-1.02.bin.vcdiff", "7526a56c580c066b3bae7b2c63df2069e315fb78610e39776bc54f6f16b3670b", "83c8093ba4bcc31e8aae4d227b278d141db78e859c0f9da6da730e9fa2aa9ff8", 410040)
        ], IsSelf: true),
        new("data/data1.psarc",
        [
            new("data1-1.02.psarc.vcdiff", "62a0e11ff75ead1da0826f77869b8ed4f964ece6eba901ca4410eab328bceb83", "d5ad5386898f5c515fd493b1253c4038baba8e90b6651a70f71e7ef9558348ab", 886537)
        ], IsOptional: true)
    ]);
}
