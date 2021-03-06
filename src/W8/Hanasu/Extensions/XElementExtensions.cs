﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Hanasu.Extensions
{
    public static class XElementExtensions
    {
        public static bool ContainsElement(this XElement element, XName name)
        {
            try
            {
                var d = element.Element(name);

                if (d == null)
                    return false;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
