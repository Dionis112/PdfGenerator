<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema"
  xmlns:altova="http://www.altova.com/xslt-extensions" exclude-result-prefixes="xs altova">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/operations">
    <result>
      <xsl:for-each select="*">
        <operation position="{position()}" type="{local-name()}" id="{@id}">
          <xsl:choose>
            <xsl:when test="self::reset"><xsl:value-of select="altova:reset-auto-number(string(@id))"/></xsl:when>
            <xsl:otherwise><xsl:value-of select="altova:generate-auto-number(string(@id), xs:integer(@start), xs:integer(@increment), string(@group))"/></xsl:otherwise>
          </xsl:choose>
        </operation>
      </xsl:for-each>
    </result>
  </xsl:template>
</xsl:stylesheet>
