<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:fo="http://www.w3.org/1999/XSL/Format"
  xmlns:altova="http://www.altova.com/xslt-extensions" exclude-result-prefixes="altova">
  <xsl:include href="../basic/parts/layout.xsl"/>
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/invoice">
    <fo:root>
      <xsl:call-template name="layout"/>
      <fo:page-sequence master-reference="a4">
        <fo:flow flow-name="xsl-region-body" font-family="sans-serif" font-size="10pt">
          <fo:block font-size="16pt"><xsl:value-of select="concat(@title, ' № ', @number)"/></fo:block>
          <xsl:for-each select="lines/line">
            <fo:block><xsl:value-of select="concat(position(), '. ', ., ' — ', format-number(number(@amount), '0.00'))"/></fo:block>
          </xsl:for-each>
          <fo:block font-weight="bold">Итого: <xsl:value-of select="format-number(altova:evaluate(string(@total-expression)), '0.00')"/></fo:block>
          <xsl:for-each select="altova:distinct-nodes(notes/note)">
            <fo:block><xsl:value-of select="."/></fo:block>
          </xsl:for-each>
        </fo:flow>
      </fo:page-sequence>
    </fo:root>
  </xsl:template>
</xsl:stylesheet>
