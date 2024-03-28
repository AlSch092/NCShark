﻿// *
// * Copyright (C) 2008 Roger Alsing : http://www.RogerAlsing.com
// *
// * This library is free software; you can redistribute it and/or modify it
// * under the terms of the GNU Lesser General Public License 2.1 or later, as
// * published by the Free Software Foundation. See the included license.txt
// * or http://www.gnu.org/copyleft/lesser.html for details.
// *

namespace Alsing.SourceCode.SyntaxDocumentParsers
{
	public sealed class NewParser : ParserBase
	{
        public override void ParseRow(int rowIndex, bool parseKeywords)
        {
            if (!parseKeywords)
                ParseLineStructure(rowIndex);
            else
                ParseLineFully(rowIndex);
        }

        private void ParseLineFully(int rowIndex)
        {
        }

        private void ParseLineStructure(int rowIndex)
        {
        }
    }
}
