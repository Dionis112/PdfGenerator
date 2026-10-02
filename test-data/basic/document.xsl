<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema"
  xmlns:fo="http://www.w3.org/1999/XSL/Format"
  exclude-result-prefixes="xs">
  <xsl:include href="parts/layout.xsl"/>
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/document">
    <fo:root>
      <xsl:call-template name="layout"/>
      <fo:page-sequence master-reference="a4">
        <fo:flow flow-name="xsl-region-body" font-family="sans-serif" font-size="10pt">
          <fo:block font-size="16pt"><xsl:value-of select="title"/></fo:block>
          <xsl:for-each-group select="items/item" group-by="@category">
            <fo:block>
              <xsl:text>Категория </xsl:text>
              <xsl:value-of select="current-grouping-key()"/>
              <xsl:text>: </xsl:text>
              <xsl:value-of select="string-join(current-group()/string(), ', ')"/>
              <xsl:text>; сумма </xsl:text>
              <xsl:value-of select="format-number(sum(current-group()/@amount/xs:decimal(.)), '0.00')"/>
            </fo:block>
          </xsl:for-each-group>
        </fo:flow>
      </fo:page-sequence>
    </fo:root>
  </xsl:template>
</xsl:stylesheet>
