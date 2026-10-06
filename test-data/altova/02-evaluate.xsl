<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:p="urn:altova-fixture:product"
  xmlns:altova="http://www.altova.com/xslt-extensions" exclude-result-prefixes="xs p altova">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/report">
    <xsl:variable name="label" select="string(expressions/@label)"/>
    <result>
      <total><xsl:value-of select="altova:evaluate(string(expressions/@total))"/></total>
      <selected><xsl:copy-of select="altova:evaluate(string(expressions/@selected), xs:decimal('20'))"/></selected>
      <labels><xsl:for-each select="p:items/p:item">
        <label><xsl:value-of select="altova:evaluate($label)"/></label>
      </xsl:for-each></labels>
      <parameters><xsl:value-of select="altova:evaluate('$p3, $p2, $p1', 7, 'текст', (2, 4))" separator="|"/></parameters>
      <node-parameter><xsl:value-of select="altova:evaluate('$p1/@code', p:items/p:item[1])"/></node-parameter>
      <empty count="{count(altova:evaluate('()'))}"/>
    </result>
  </xsl:template>
</xsl:stylesheet>
