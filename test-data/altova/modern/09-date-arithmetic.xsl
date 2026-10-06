<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
  exclude-result-prefixes="xs a">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/cases">
    <result>
      <xsl:for-each select="case">
        <xsl:variable name="value" select="xs:date(@value)"/>
        <xsl:variable name="delta" select="xs:integer(@delta)"/>
        <case id="{@id}" input="{@value}" delta="{@delta}">
          <years><xsl:value-of select="a:add-years-to-date($value, $delta)"/></years>
          <months><xsl:value-of select="a:add-months-to-date($value, $delta)"/></months>
          <days><xsl:value-of select="a:add-days-to-date($value, $delta)"/></days>
        </case>
      </xsl:for-each>
    </result>
  </xsl:template>
</xsl:stylesheet>
