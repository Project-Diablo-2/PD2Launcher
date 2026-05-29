using Org.BouncyCastle.Crypto.Digests;

namespace PD2Shared.Extensions
{
    public static class GeneralDigestEx
    {
        public static byte[] DoFinalAndReturn(this GeneralDigest digest)
        {
            var res = new byte[digest.GetDigestSize()];

            digest.DoFinal(res, outOff: 0);

            return res;
        }
    }
}
