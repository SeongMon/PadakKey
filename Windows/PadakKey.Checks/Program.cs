using System.Text;
using PadakKey.Core;
static string Base32(byte[] data) { const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; int buffer = 0, bits = 0; var result = new StringBuilder(); foreach (byte b in data) { buffer = (buffer << 8) | b; bits += 8; while (bits >= 5) { bits -= 5; result.Append(alphabet[(buffer >> bits) & 31]); } } if (bits > 0) result.Append(alphabet[(buffer << (5 - bits)) & 31]); return result.ToString(); }
long[] times = [59, 1111111109, 1111111111, 1234567890, 2000000000, 20000000000];
string[][] expected = [["94287082","07081804","14050471","89005924","69279037","65353130"],["46119246","68084774","67062674","91819424","90698825","77737706"],["90693936","25091201","99943326","93441116","38618901","47863826"]];
string[] algorithms = ["SHA1","SHA256","SHA512"];
int[] lengths = [20,32,64];
for (int a = 0; a < 3; a++) { string raw = string.Concat(Enumerable.Repeat("1234567890",7))[..lengths[a]]; var account = new Account(Guid.NewGuid(),"Test","RFC",Base32(Encoding.ASCII.GetBytes(raw)),algorithms[a],8); for (int i=0;i<times.Length;i++) if(account.Code(times[i])!=expected[a][i]) throw new Exception("RFC vector failed"); }
var valid = Otp.Parse("otpauth://totp/Example:alice?secret=JBSWY3DPEHPK3PXP&issuer=Example");
if(valid.Name!="alice" || valid.Code(29)==valid.Code(30) || valid.Code(1)!=valid.Code(29)) throw new Exception("Boundary/URI failure");
foreach(string uri in new[]{"otpauth://hotp/a?secret=JBSWY3DPEHPK3PXP", "otpauth://totp/a?secret=BAD!", "otpauth://totp/a?secret=JBSWY3DPEHPK3PXP&period=0", "otpauth://totp/a?secret=JBSWY3DPEHPK3PXP&secret=AAAA", "otpauth://totp/X:a?issuer=Y&secret=JBSWY3DPEHPK3PXP"}) { bool failed=false; try{Otp.Parse(uri);}catch{failed=true;}if(!failed)throw new Exception("Invalid URI accepted"); }
Console.WriteLine("PASS: 18 RFC6238 vectors, period boundaries, URI validation");
